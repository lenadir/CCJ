@echo off
rem ============================================================
rem  Builds dist\CCJ-Setup.exe: one self-contained installer file.
rem
rem  It first runs build.cmd, then compiles installer\*.cs with the
rem  application, its two DLLs and the documentation embedded as resources.
rem  Needs only .NET Framework 4.8. Visual Studio is not required.
rem ============================================================

setlocal enableextensions
set "ROOT=%~dp0"
set "DIST=%ROOT%dist"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" goto NO_CSC

echo.
echo === 1/3  Building the application
call "%ROOT%build.cmd" Release
if errorlevel 1 goto APP_FAILED

echo.
echo === 2/3  Collecting the files to embed
copy /y "%ROOT%README.md" "%DIST%\README.md" >nul
copy /y "%ROOT%USER-MANUAL.md" "%DIST%\USER-MANUAL.md" >nul
copy /y "%ROOT%INSTALL.md" "%DIST%\INSTALL.md" >nul

for %%F in (CCJ.exe CCJ.exe.config Microsoft.Win32.TaskScheduler.dll Newtonsoft.Json.dll README.md USER-MANUAL.md INSTALL.md) do (
  if not exist "%DIST%\%%F" goto MISSING_PAYLOAD
)

echo.
echo === 3/3  Compiling CCJ-Setup.exe
"%CSC%" /nologo /target:winexe /optimize+ /warn:4 ^
  /out:"%DIST%\CCJ-Setup.exe" ^
  /win32manifest:"%ROOT%installer\setup.manifest" ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  /resource:"%DIST%\CCJ.exe",CCJ.Setup.Payload.CCJ.exe ^
  /resource:"%DIST%\CCJ.exe.config",CCJ.Setup.Payload.CCJ.exe.config ^
  /resource:"%DIST%\Microsoft.Win32.TaskScheduler.dll",CCJ.Setup.Payload.Microsoft.Win32.TaskScheduler.dll ^
  /resource:"%DIST%\Newtonsoft.Json.dll",CCJ.Setup.Payload.Newtonsoft.Json.dll ^
  /resource:"%DIST%\USER-MANUAL.md",CCJ.Setup.Payload.USER-MANUAL.md ^
  /resource:"%DIST%\README.md",CCJ.Setup.Payload.README.md ^
  /resource:"%DIST%\INSTALL.md",CCJ.Setup.Payload.INSTALL.md ^
  "%ROOT%installer\Installer.cs" "%ROOT%installer\SetupForm.cs"
if errorlevel 1 goto SETUP_FAILED

echo.
echo Built: %DIST%\CCJ-Setup.exe
for %%S in ("%DIST%\CCJ-Setup.exe") do echo Size:  %%~zS bytes
echo.
echo Hand that one file to the other PC. Double-click it there.
endlocal & exit /b 0

:NO_CSC
echo The C# compiler was not found. Install .NET Framework 4.8.
endlocal & exit /b 1

:APP_FAILED
echo The application build failed, so no installer was made.
endlocal & exit /b 1

:MISSING_PAYLOAD
echo Expected file is missing from dist\. The application build is incomplete.
endlocal & exit /b 1

:SETUP_FAILED
echo The installer failed to compile.
endlocal & exit /b 1
