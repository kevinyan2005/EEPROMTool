//; $File: $
//; $Revision: $
//; $Change: $
//; $DateTime: $
//; $Author: $
//; $Copyright: (c) 2015 by Monteris Medical, Inc.  All rights reserved. $
//; $FileDescription: $

//;From: http://stackoverflow.com/questions/20174359/replace-a-text-in-a-file-with-inno-setup

[Code]
function FileReplaceString(const FileName, SearchString, ReplaceString: string): boolean;
var
  MyFile : TStrings;
  MyText : string;
begin
  MyFile := TStringList.Create;

  try
    result := true;

    try
      MyFile.LoadFromFile(FileName);
      MyText := MyFile.Text;

      if StringChangeEx(MyText, SearchString, ReplaceString, True) > 0 then //Only save if text has been changed.
      begin;
        MyFile.Text := MyText;
        MyFile.SaveToFile(FileName);
      end;
    except
      result := false;
    end;
  finally
    MyFile.Free;
  end;
end;