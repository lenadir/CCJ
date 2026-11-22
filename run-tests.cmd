@echo off
rem ============================================================
rem  Builds and runs the unit tests, then the self test.
rem
rem    run-tests.cmd                 everything
rem    run-tests.cmd --where "..."   passes NUnitLite options through
rem
rem  The tests are a self running NUnitLite console application, because there is no
rem  vstest.console.exe without Visual Studio. Every test uses folders under %TEMP%;
rem  no network path is touched.
rem ============================================================

setlocal enableextensions
set "ROOT=%~dp0"

call "%ROOT%build.cmd" Release
if errorlevel 1 goto BUILD_FAILED

echo.
echo === Unit tests
"%ROOT%CCJ.Tests\bin\Release\CCJ.Tests.exe" --noresult %*
if errorlevel 1 goto TESTS_FAILED

echo.
echo === Self test of the built exe (generates and runs a dry run script under %%TEMP%%)
"%ROOT%dist\CCJ.exe" --selftest
if errorlevel 1 goto SELFTEST_FAILED

echo.
echo All tests passed.
endlocal & exit /b 0

:BUILD_FAILED
echo.
echo The build failed, so no tests were run.
endlocal & exit /b 1

:TESTS_FAILED
echo.
echo Unit tests failed. See the list above.
endlocal & exit /b 1

:SELFTEST_FAILED
echo.
echo The self test failed. See the list above.
endlocal & exit /b 1
