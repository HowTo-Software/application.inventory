@echo off
:: ======================================
:: Computer Inventory Agent Installer
:: ======================================
:: Tries PowerShell installer first (full featured)
:: Falls back to native CMD installer if PowerShell fails
:: If both fail, cleans up and retries with native installer

setlocal enabledelayedexpansion

:: Get the directory where this script is located
set "SCRIPT_DIR=%~dp0"
set "RETRY_COUNT=0"
set "MAX_RETRIES=1"

:: Check for admin rights
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo.
    echo ======================================
    echo  Administrator Rights Required
    echo ======================================
    echo.
    echo Please right-click this file and select "Run as administrator"
    echo.
    timeout /t 10 /nobreak
    exit /b 1
)

:: Parse command line arguments
set "SERVER_URL="
set "API_KEY="
set "FORCE_NATIVE=0"

:parse_args
if "%~1"=="" goto run_installer
if /i "%~1"=="-ServerUrl" (
    set "SERVER_URL=%~2"
    shift
    shift
    goto parse_args
)
if /i "%~1"=="-ApiKey" (
    set "API_KEY=%~2"
    shift
    shift
    goto parse_args
)
if /i "%~1"=="-Native" (
    set "FORCE_NATIVE=1"
    shift
    goto parse_args
)
shift
goto parse_args

:run_installer
:: Build arguments for installers
set "INSTALL_ARGS="
if defined SERVER_URL set "INSTALL_ARGS=!INSTALL_ARGS! -ServerUrl "%SERVER_URL%""
if defined API_KEY set "INSTALL_ARGS=!INSTALL_ARGS! -ApiKey "%API_KEY%""

:: If user forced native, skip PowerShell
if %FORCE_NATIVE% equ 1 (
    echo.
    echo [INFO] Native mode requested - skipping PowerShell installer
    echo.
    goto run_native
)

:run_powershell
:: Try PowerShell installer first
echo Unblocking downloaded files...
powershell -Command "Get-ChildItem -Path '%SCRIPT_DIR%' -Recurse | Unblock-File" 2>nul

:: Build the PowerShell command with arguments (no extra quotes - PowerShell handles it)
set "PS_ARGS="
if defined SERVER_URL set "PS_ARGS=!PS_ARGS! -ServerUrl %SERVER_URL%"
if defined API_KEY set "PS_ARGS=!PS_ARGS! -ApiKey %API_KEY%"

:: Run the PowerShell installer with execution policy bypass
echo Running PowerShell installer...
echo.
powershell -ExecutionPolicy Bypass -NoProfile -File "%SCRIPT_DIR%install.ps1" %PS_ARGS%
set "PS_RESULT=!errorLevel!"

:: Check if agent is running after PowerShell install (check scheduled task or process)
tasklist /FI "IMAGENAME eq MKS.InventoryAgent.exe" 2>nul | find /I "MKS.InventoryAgent.exe" >nul 2>&1
if !errorLevel! equ 0 (
    echo.
    echo Installation successful!
    exit /b 0
)

:: Check if scheduled task exists as fallback
schtasks /Query /TN "ComputerInventoryAgent" >nul 2>&1
if !errorLevel! equ 0 (
    echo.
    echo Installation successful! (Scheduled task created, process may start shortly)
    exit /b 0
)

:: PowerShell didn't get agent running, try native fallback
echo.
echo PowerShell installer did not fully succeed, trying native CMD fallback...
echo.
goto run_native

:run_native
:: Try native CMD installer as fallback
if exist "%SCRIPT_DIR%install-native.cmd" (
    echo Running native installer...
    echo.
    call "%SCRIPT_DIR%install-native.cmd" %INSTALL_ARGS%
    set "NATIVE_RESULT=!errorLevel!"
    
    :: Check if agent is running (process or scheduled task)
    tasklist /FI "IMAGENAME eq MKS.InventoryAgent.exe" 2>nul | find /I "MKS.InventoryAgent.exe" >nul 2>&1
    if !errorLevel! equ 0 (
        echo.
        echo Native installation successful!
        exit /b 0
    )
    
    :: Check if scheduled task exists as fallback
    schtasks /Query /TN "ComputerInventoryAgent" >nul 2>&1
    if !errorLevel! equ 0 (
        echo.
        echo Native installation successful! (Scheduled task created, process may start shortly)
        exit /b 0
    )
    
    :: Agent not running - try cleanup and retry if we haven't already
    if !RETRY_COUNT! lss %MAX_RETRIES% (
        echo.
        echo Agent not running after native install - performing clean reinstall...
        echo.
        goto cleanup_and_retry
    )
    
    echo.
    echo Installation completed but agent is not running.
    echo Try running the executable directly to diagnose:
    echo   "%ProgramFiles%\ComputerInventory\Agent\MKS.InventoryAgent.exe"
    echo.
    exit /b !NATIVE_RESULT!
) else (
    echo.
    echo Native installer not found.
    echo Installation may be incomplete - service is not running.
    echo.
    timeout /t 30 /nobreak
    exit /b 1
)

exit /b 0

:cleanup_and_retry
:: ======================================
:: Clean Reinstall - Remove everything and retry with native
:: ======================================
set /a RETRY_COUNT+=1
echo.
echo ======================================
echo  Clean Reinstall Attempt !RETRY_COUNT!
echo ======================================
echo.

:: Stop and remove scheduled task (new method)
echo Stopping scheduled task...
schtasks /End /TN "ComputerInventoryAgent" >nul 2>&1
timeout /t 2 /nobreak >nul

echo Removing scheduled task...
schtasks /Delete /TN "ComputerInventoryAgent" /F >nul 2>&1
timeout /t 1 /nobreak >nul

:: Stop and remove service (legacy cleanup)
echo Stopping service (if exists)...
sc stop ComputerInventoryAgent >nul 2>&1
timeout /t 2 /nobreak >nul

echo Removing service (if exists)...
sc delete ComputerInventoryAgent >nul 2>&1
timeout /t 2 /nobreak >nul

:: Wait for service to be fully removed (if it existed)
echo Waiting for cleanup...
:wait_service_removed
sc query ComputerInventoryAgent >nul 2>&1
if !errorLevel! equ 0 (
    timeout /t 1 /nobreak >nul
    goto wait_service_removed
)

:: Remove installation files
echo Removing installation files...
set "INSTALL_DIR=%ProgramFiles%\ComputerInventory\Agent"
if exist "!INSTALL_DIR!" (
    :: Kill any running process first
    taskkill /F /IM MKS.InventoryAgent.exe >nul 2>&1
    timeout /t 2 /nobreak >nul
    
    :: Remove files but keep logs folder
    for %%F in ("!INSTALL_DIR!\*.exe" "!INSTALL_DIR!\*.dll" "!INSTALL_DIR!\*.json" "!INSTALL_DIR!\*.pdb" "!INSTALL_DIR!\*.config") do (
        del /F /Q "%%F" >nul 2>&1
    )
    if exist "!INSTALL_DIR!\runtimes" rd /S /Q "!INSTALL_DIR!\runtimes" >nul 2>&1
)

:: Remove any CI policy markers that might cause issues
if exist "%ProgramData%\ComputerInventory\ci-policy-installed" (
    echo Removing CI policy marker...
    del /F /Q "%ProgramData%\ComputerInventory\ci-policy-installed" >nul 2>&1
)

echo.
echo Clean-up complete. Retrying native installation...
echo.
timeout /t 2 /nobreak >nul

:: Force native mode for retry
set "FORCE_NATIVE=1"
goto run_native
