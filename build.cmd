@echo off
rem ============================================================
rem  Builds CCJ and copies the release output to dist\.
rem
rem    build.cmd            builds Release
rem    build.cmd Debug      builds Debug
rem
rem  Needs only .NET Framework 4.8, which supplies MSBuild.exe.
rem  Visual Studio is not required.
rem ============================================================

setlocal enableextensions
set "ROOT=%~dp0"
set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Release"

echo.
echo === 1/3  Restoring packages
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%tools\restore-packages.ps1"
if errorlevel 1 goto RESTORE_FAILED

rem Prefer a Visual Studio MSBuild when one is installed, otherwise use the in-box one.
set "MSBUILD="
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSWHERE%" (
  for /f "usebackq delims=" %%M in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set "MSBUILD=%%M"
)
if not defined MSBUILD set "MSBUILD=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if not exist "%MSBUILD%" set "MSBUILD=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
if not exist "%MSBUILD%" goto NO_MSBUILD

echo.
echo === 2/3  Building %CONFIG% with %MSBUILD%
"%MSBUILD%" "%ROOT%CCJ.sln" /t:Rebuild /p:Configuration=%CONFIG% /v:minimal /nologo
if errorlevel 1 goto BUILD_FAILED

echo.
echo === 3/3  Copying to dist\
if not exist "%ROOT%dist" mkdir "%ROOT%dist"
copy /y "%ROOT%CCJ\bin\%CONFIG%\CCJ.exe" "%ROOT%dist\" >nul
if errorlevel 1 goto COPY_FAILED
copy /y "%ROOT%CCJ\bin\%CONFIG%\CCJ.exe.config" "%ROOT%dist\" >nul
copy /y "%ROOT%CCJ\bin\%CONFIG%\Microsoft.Win32.TaskScheduler.dll" "%ROOT%dist\" >nul
copy /y "%ROOT%CCJ\bin\%CONFIG%\Newtonsoft.Json.dll" "%ROOT%dist\" >nul

echo.
echo Build finished. Run dist\CCJ.exe
echo.
dir /b "%ROOT%dist"
endlocal & exit /b 0

:RESTORE_FAILED
echo.
echo Packages could not be restored, so nothing was built.
endlocal & exit /b 1

:NO_MSBUILD
echo.
echo MSBuild.exe was not found. Install the .NET Framework 4.8 developer pack or Visual Studio.
endlocal & exit /b 1

:BUILD_FAILED
echo.
echo The build failed. See the messages above.
endlocal & exit /b 1

:COPY_FAILED
echo.
echo The build succeeded but CCJ.exe could not be copied to dist\.
endlocal & exit /b 1
