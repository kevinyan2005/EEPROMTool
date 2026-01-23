[Code]
//;Called just before Setup terminates. Copies the log file to C:\
procedure DeinitializeSetup();
var
  logfilepathname, logfilename, newfilepathname, timestamp : string;
begin
  logfilepathname := expandconstant('{log}');
  //; If logfile is disabled then logfilepathname is empty
  if logfilepathname = '' then begin
     exit;
  end;
  logfilename := ExtractFileName(logfilepathname);
  newfilepathname := 'C:\'
  //; Make sure the destination path exists.
  ForceDirectories(newfilepathname); 
  timestamp := GetDateTimeString('yyyy-mm-dd hh.nn.ss', ' ', ' ')
  newfilepathname := newfilepathname + timestamp + ' - {#partNumber} Install Log.txt'; 
  filecopy(logfilepathname, newfilepathname, false);
  DeleteFile(logfilepathname);
end;