  [Code]
  function CheckProcessRunning( aProcName,
                                aProcDesc: string ): boolean;   
  var
    ShellResult: boolean;
    ResultCode: integer;
    cmd: string;
    sl: TStringList;
    f: string;
    d: string;
  begin
    cmd := 'for /f "delims=," %%i ' + 
           'in (''tasklist /FI "IMAGENAME eq ' + aProcName + '" /FO CSV'') ' + 
           'do if "%%~i"=="' + aProcName + '" exit 1'; 
    f := 'CheckProc.cmd';
    d := AddBackSlash( ExpandConstant( '{tmp}' ));
    sl := TStringList.Create;
    sl.Add( cmd );
    sl.Add( 'exit /0' );
    sl.SaveToFile( d + f );
    sl.Free;
    Result := true;
    while ( Result ) do
    begin
      ResultCode := 1;
      ShellResult := Exec( f,
                           '',
                           d, 
                           SW_HIDE, 
                           ewWaitUntilTerminated, 
                           ResultCode );
      Result := ResultCode > 0;
      if Result then 
      begin
        ResultCode := MsgBox( aProcDesc + ' is running. This program must be closed to continue.'#13#10 + 
            'Close the program and click Retry to continue, click Ignore to automatically attempt to close the program and continue, or click Abort to cancel.', 
             mbConfirmation, 
             MB_ABORTRETRYIGNORE );
        if (ResultCode = IDABORT) then
        begin
          Break;
        end else if (ResultCode = IDIGNORE) then
        begin
          Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im ' + aProcName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
        end;
      end;
    end;
    DeleteFile( d + f );
  end;