[Code]
//;Gets the Uninstaller path for a part from the registry
function GetUninstallerPath(Param : string): string;
var
	RegKey: string;
begin
	Result := '';
	RegKey := 'SOFTWARE\Monteris Medical Inc.\Uninstall\' + Param + '\';
	RegQueryStringValue(HKLM64, RegKey, 'UninstallString', Result);
end;