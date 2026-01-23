# Utility to find missing included dll references in csproj
# the tool is capable to introspect into each packages content, then also to identive .net dll's only
# then reports only error on the projects where it found a package that contains c# dll's 
#
# How to use:
# powershell .\CheckCsProjPackages.ps1 <my directory>
# if directory is omitted the the current path is assumed to be the root solution dir


param (
    [string]$solutionRoot
)

# If the solution root is not provided as an argument, use the current directory
if (-not $solutionRoot) {
    $solutionRoot = Get-Location
    #$solutionRoot = 'C:\src\Depot\main\UTs\MVisionTestFramework\'
}

# Display the full path of the solution root
Write-Output "Solution root directory: $solutionRoot"

# Function to check if a directory should be excluded
function ShouldExcludeFolder {
    param (
        [System.IO.DirectoryInfo]$folder
    )
    return ($folder.Attributes -band [System.IO.FileAttributes]::Hidden) -or ($folder.Name -like '.*')
}

# Function to get the latest version directory for a package
function Get-LatestVersionDirectory {
    param (
        [string]$packageId
    )
    $packageDirs = Get-ChildItem -Path (Join-Path -Path $solutionRoot -ChildPath "packages") -Directory | Where-Object { $_.Name -like "$packageId.*" }
    if ($packageDirs.Count -gt 0) {
        $latestDir = $packageDirs | Sort-Object { 
            $versionMatch = [regex]::Match($_.Name, '(?<=\.)\d+\.\d+\.\d+(\.\d+)?$')
            if ($versionMatch.Success) {
                try {
                    return [version]$versionMatch.Value
                } catch {
                    return [version]"0.0.0.0"
                }
            } else {
                return [version]"0.0.0.0"
            }
        } -Descending | Select-Object -First 1
        return $latestDir.FullName
    }
    return $null
}

# Function to extract package ID from HintPath
function GetPackageIdFromHintPath {
    param (
        [string]$hintPath
    )
    $packagePart = ($hintPath -split '\\packages\\')[1]
    $packageIdFull = ($packagePart -split '\\')[0]
    $packageId = ($packageIdFull -split '.[0-9]+\.[0-9]+\.[0-9]+(\.[0-9]+)?')[0]
    return $packageId
}

# Function to check if a package contains .NET DLLs
function ContainsDotNetDll {
    param (
        [string]$packageId
    )
    $packageDir = Get-LatestVersionDirectory -packageId $packageId
    if ($packageDir) {
        $libDir = Join-Path -Path $packageDir -ChildPath "lib"
        if (Test-Path $libDir) {
            $netVersions = @("net20", "net35", "net40", "net40-client", "net45", "net451", "net46", "net461", "net47", "net472", "netstandard1.0", "netstandard2.0")
            foreach ($netVersion in $netVersions) {
                $dllPath = Join-Path -Path $libDir -ChildPath "$netVersion"
                if (Test-Path $dllPath) {
                    $dllFiles = Get-ChildItem -Path $dllPath -Filter "*.dll" -Recurse
                    if ($dllFiles.Count -gt 0) {
                        return $true
                    }
                }
            }
        }
    }
    return $false
}

# Get all packages.config files in the solution, excluding hidden and dot-prefixed folders
$packagesConfigFiles = Get-ChildItem -Path $solutionRoot -Recurse -Filter "packages.config" | Where-Object { -not (ShouldExcludeFolder $_.Directory) }

foreach ($packagesConfigFile in $packagesConfigFiles) {
    # Load the packages.config file
    [xml]$packagesConfig = Get-Content $packagesConfigFile.FullName
    
    # Extract package IDs from packages.config and filter those that contain .NET DLLs
    $packageIds = $packagesConfig.packages.package | ForEach-Object {
        $packageId = $_.id
        if (ContainsDotNetDll -packageId $packageId) {
            $packageId
        }
    } | Where-Object { $_ -ne $null } | Sort-Object -Unique

    # Find the corresponding .csproj file
    $csprojFile = Get-ChildItem -Path $packagesConfigFile.DirectoryName -Filter "*.csproj" | Select-Object -First 1
    
    if ($csprojFile) {
        # Load the .csproj file
        [xml]$csproj = Get-Content $csprojFile.FullName
        
        # Extract references from the .csproj file using HintPath and remove duplicates and empty lines
        $references = $csproj.Project.ItemGroup.Reference | ForEach-Object {
            if ($_.HintPath) {
                $packageId = GetPackageIdFromHintPath -hintPath $_.HintPath
                if ($packageId) { $packageId }
            }
        } | Sort-Object -Unique
        
        # Initialize lists for found and missing references
        $foundReferences = @()
        $missingReferences = @()

        # Check each package reference
        foreach ($packageId in $packageIds) {
            if ($references -contains $packageId) {
                $foundReferences += $packageId
            } else {
                if (ContainsDotNetDll -packageId $packageId) {
                    $missingReferences += $packageId
                } else {
                    $foundReferences += $packageId
                }
            }
        }

        # Log missing references
        if ($missingReferences.Count -gt 0) {
            Write-Host "Project: $($csprojFile.FullName)" -ForegroundColor Yellow
            Write-Host "Missing references:" -ForegroundColor Red
            $missingReferences | ForEach-Object { Write-Host $_ -ForegroundColor Red }
            Write-Output ""
        } else {
            Write-Host "Project: $($csprojFile.FullName) - Passed." -ForegroundColor Green
        }
    } else {
        Write-Host "No .csproj file found in $($packagesConfigFile.DirectoryName)" -ForegroundColor Yellow
    }
}
