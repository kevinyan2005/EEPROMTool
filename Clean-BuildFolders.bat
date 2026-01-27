@echo off
setlocal enabledelayedexpansion

echo Cleaning bin and obj folders...

REM Loop through all folders named bin or obj recursively
for /d /r %%G in (*) do (
    if /I "%%~nxG"=="bin" (
        echo Deleting %%G
        rmdir /s /q "%%G"
    )
    if /I "%%~nxG"=="obj" (
        echo Deleting %%G
        rmdir /s /q "%%G"
    )
)

echo Cleanup complete!
pause