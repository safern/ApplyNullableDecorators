@echo off
setlocal

set SLN=%~dp0apply-nullable-decorators.sln
set BIN=%~dp0bin\
set CONFIG=release

dotnet build %SLN% -c %CONFIG% -o=%BIN% /nologo /p:BuildGlobalTool=true