@echo off
rem Build ExplorerTabs with the C# compiler bundled with .NET Framework 4 (no Visual Studio needed)
cd /d %~dp0
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo csc.exe not found: %CSC%
  echo Please make sure .NET Framework 4.x ^(64-bit^) is installed.
  exit /b 1
)
if not exist bin mkdir bin
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /out:bin\ExplorerTabs.exe src\ExplorerTabs.cs
if errorlevel 1 exit /b 1
echo Built: bin\ExplorerTabs.exe
