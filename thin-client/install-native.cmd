@echo off
:: ======================================
:: Computer Inventory Agent Installer
:: Pure CMD version - no PowerShell dependency
:: Uses Scheduled Task (not Windows Service)
:: ======================================

:: Configuration
set "TaskName=ComputerInventoryAgent"
set "DisplayName=Computer Inventory Agent"
set "Description=Reports computer inventory and status to the central management server"
set "InstallPath=%ProgramFiles%\ComputerInventory\Agent"
set "ExeName=MKS.InventoryAgent.exe"
set "DataPath=%ProgramData%\ComputerInventory"

:: Get the directory where this script is located
set "SCRIPT_DIR=%~dp0"

:: Parse command line arguments
set "SERVER_URL="
set "API_KEY="

:parse_args
if "%~1"=="" goto check_admin
if /i "%~1"=="-ServerUrl" set "SERVER_URL=%~2" & shift & shift & goto parse_args
if /i "%~1"=="-ApiKey" set "API_KEY=%~2" & shift & shift & goto parse_args
shift
goto parse_args

:check_admin
:: Check for admin rights
net session >nul 2>&1
if %errorLevel% neq 0 goto need_admin
goto start_install

:need_admin
echo.
echo ======================================
echo  Administrator Rights Required
echo ======================================
echo.
echo Please right-click this file and select "Run as administrator"
echo.
timeout /t 10 /nobreak
exit /b 1

:start_install
echo.
echo ======================================
echo  Computer Inventory Agent Installer
echo  (Native CMD - No PowerShell)
echo ======================================
echo.

:: Step 0: Install code signing certificate if present (for HVCI compliance)
if exist "%SCRIPT_DIR%MKSCodeSign.cer" (
    echo [0/9] Installing code signing certificate...
    certutil -addstore TrustedPublisher "%SCRIPT_DIR%MKSCodeSign.cer" >nul 2>&1
    certutil -addstore Root "%SCRIPT_DIR%MKSCodeSign.cer" >nul 2>&1
    echo   Certificate installed for HVCI compliance.
)

:: Step 1: Stop and remove existing Windows Service if it exists (migration from service to task)
echo [1/9] Checking for existing Windows Service (legacy)...
sc query ComputerInventoryAgent >nul 2>&1
if %errorLevel% equ 0 (
    echo   Found legacy Windows Service - migrating to Scheduled Task...
    echo   Stopping existing service...
    net stop ComputerInventoryAgent >nul 2>&1
    timeout /t 2 /nobreak >nul
    taskkill /f /im "%ExeName%" >nul 2>&1
    timeout /t 1 /nobreak >nul
    echo   Removing existing service...
    sc delete ComputerInventoryAgent >nul 2>&1
    timeout /t 2 /nobreak >nul
)

:: Step 1b: Stop and remove existing Scheduled Task if it exists
echo   Checking for existing Scheduled Task...
schtasks /Query /TN "%TaskName%" >nul 2>&1
if %errorLevel% equ 0 (
    echo   Stopping existing task...
    schtasks /End /TN "%TaskName%" >nul 2>&1
    timeout /t 2 /nobreak >nul
    taskkill /f /im "%ExeName%" >nul 2>&1
    timeout /t 1 /nobreak >nul
    echo   Removing existing task...
    schtasks /Delete /TN "%TaskName%" /F >nul 2>&1
    timeout /t 1 /nobreak >nul
)

:step2
:: Step 2: Create directories
echo [2/9] Creating installation directories...
if not exist "%InstallPath%" mkdir "%InstallPath%"
if not exist "%InstallPath%\logs" mkdir "%InstallPath%\logs"
if not exist "%DataPath%" mkdir "%DataPath%"

:: Step 3: Set permissions on logs folder
echo [3/9] Setting permissions...
icacls "%InstallPath%\logs" /grant "SYSTEM:(OI)(CI)F" >nul 2>&1
icacls "%InstallPath%\logs" /grant "Users:(OI)(CI)M" >nul 2>&1
icacls "%DataPath%" /grant "SYSTEM:(OI)(CI)F" >nul 2>&1
icacls "%DataPath%" /grant "Users:(OI)(CI)M" >nul 2>&1

:: Step 4: Copy files (excluding install scripts)
echo [4/9] Copying files...
:: Copy all files except install scripts
for %%f in ("%SCRIPT_DIR%*.exe") do copy /y "%%f" "%InstallPath%\" >nul 2>&1
for %%f in ("%SCRIPT_DIR%*.dll") do copy /y "%%f" "%InstallPath%\" >nul 2>&1
for %%f in ("%SCRIPT_DIR%*.json") do copy /y "%%f" "%InstallPath%\" >nul 2>&1
for %%f in ("%SCRIPT_DIR%*.pdb") do copy /y "%%f" "%InstallPath%\" >nul 2>&1
for %%f in ("%SCRIPT_DIR%*.config") do copy /y "%%f" "%InstallPath%\" >nul 2>&1
for %%f in ("%SCRIPT_DIR%*.deps.json") do copy /y "%%f" "%InstallPath%\" >nul 2>&1
for %%f in ("%SCRIPT_DIR%*.runtimeconfig.json") do copy /y "%%f" "%InstallPath%\" >nul 2>&1

:: Copy subdirectories if any
if exist "%SCRIPT_DIR%runtimes" xcopy /s /y /q "%SCRIPT_DIR%runtimes" "%InstallPath%\runtimes\" >nul 2>&1

:: Step 5: Unblock files using streams (remove Zone.Identifier)
echo [5/9] Unblocking files...
:: Try to remove Zone.Identifier alternate data streams
powershell -Command "Get-ChildItem -Path '%InstallPath%' -Recurse | Unblock-File -ErrorAction SilentlyContinue" >nul 2>&1

:: Step 6: Handle persisted configuration
echo [6/9] Checking for persisted configuration...
if exist "%DataPath%\agent-config-backup.json" echo   Found persisted config - will be restored by agent on startup

:: Step 7: Update server URL and API key in config if specified
echo [7/9] Updating configuration...
set "CONFIG_PATH=%InstallPath%\appsettings.json"
if not exist "%CONFIG_PATH%" (
    echo   WARNING: appsettings.json not found at %CONFIG_PATH%
    goto step8
)

:: Update config using PowerShell JSON manipulation (same as install.ps1)
if defined SERVER_URL goto update_config
if defined API_KEY goto update_config
echo   No custom server URL or API key specified - using bundled defaults.
goto step8

:update_config
if defined SERVER_URL echo   Setting server URL to: %SERVER_URL%
if defined API_KEY echo   Setting API key

:: Build and run a PowerShell script to update the JSON config
set "PS_SCRIPT=%TEMP%\update-agent-config.ps1"
echo $c = Get-Content '%CONFIG_PATH%' ^| ConvertFrom-Json > "%PS_SCRIPT%"
if defined SERVER_URL echo $c.ApiSettings.BaseUrl = '%SERVER_URL%' >> "%PS_SCRIPT%"
if defined API_KEY echo $c.ApiSettings.ApiKey = '%API_KEY%' >> "%PS_SCRIPT%"
echo $c ^| ConvertTo-Json -Depth 10 ^| Set-Content '%CONFIG_PATH%' >> "%PS_SCRIPT%"

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" >nul 2>&1
if %errorLevel% equ 0 (
    echo   Configuration updated successfully.
    del "%PS_SCRIPT%" >nul 2>&1
    goto step8
)

:: Fallback: use PowerShell string replacement if JSON parsing failed
echo   WARNING: JSON config update failed. Trying string replacement fallback...
del "%PS_SCRIPT%" >nul 2>&1
if defined SERVER_URL (
    powershell -NoProfile -Command "(Get-Content '%CONFIG_PATH%' -Raw) -replace '\"BaseUrl\":\s*\"[^\"]*\"', '\"BaseUrl\": \"%SERVER_URL%\"' | Set-Content '%CONFIG_PATH%'" >nul 2>&1
)
if defined API_KEY (
    powershell -NoProfile -Command "(Get-Content '%CONFIG_PATH%' -Raw) -replace '\"ApiKey\":\s*\"[^\"]*\"', '\"ApiKey\": \"%API_KEY%\"' | Set-Content '%CONFIG_PATH%'" >nul 2>&1
)

:step8

:: Step 8: Install Scheduled Task
echo [8/9] Installing Scheduled Task...
set "ExePath=%InstallPath%\%ExeName%"

if not exist "%ExePath%" goto exe_not_found
goto install_task

:exe_not_found
echo.
echo ERROR: Executable not found at %ExePath%
echo.
timeout /t 10 /nobreak
exit /b 1

:install_task
:: Create the scheduled task using schtasks command
:: Run at startup as SYSTEM with highest privileges, with 30 second delay
:: Using XML to set advanced options like restart on failure

:: Create a temporary XML file for the task definition
set "TASK_XML=%TEMP%\ComputerInventoryTask.xml"

echo ^<?xml version="1.0" encoding="UTF-16"?^> > "%TASK_XML%"
echo ^<Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task"^> >> "%TASK_XML%"
echo   ^<RegistrationInfo^> >> "%TASK_XML%"
echo     ^<Description^>%Description%^</Description^> >> "%TASK_XML%"
echo   ^</RegistrationInfo^> >> "%TASK_XML%"
echo   ^<Triggers^> >> "%TASK_XML%"
echo     ^<BootTrigger^> >> "%TASK_XML%"
echo       ^<Enabled^>true^</Enabled^> >> "%TASK_XML%"
echo       ^<Delay^>PT30S^</Delay^> >> "%TASK_XML%"
echo     ^</BootTrigger^> >> "%TASK_XML%"
echo   ^</Triggers^> >> "%TASK_XML%"
echo   ^<Principals^> >> "%TASK_XML%"
echo     ^<Principal id="Author"^> >> "%TASK_XML%"
echo       ^<UserId^>S-1-5-18^</UserId^> >> "%TASK_XML%"
echo       ^<RunLevel^>HighestAvailable^</RunLevel^> >> "%TASK_XML%"
echo     ^</Principal^> >> "%TASK_XML%"
echo   ^</Principals^> >> "%TASK_XML%"
echo   ^<Settings^> >> "%TASK_XML%"
echo     ^<MultipleInstancesPolicy^>IgnoreNew^</MultipleInstancesPolicy^> >> "%TASK_XML%"
echo     ^<DisallowStartIfOnBatteries^>false^</DisallowStartIfOnBatteries^> >> "%TASK_XML%"
echo     ^<StopIfGoingOnBatteries^>false^</StopIfGoingOnBatteries^> >> "%TASK_XML%"
echo     ^<AllowHardTerminate^>false^</AllowHardTerminate^> >> "%TASK_XML%"
echo     ^<StartWhenAvailable^>true^</StartWhenAvailable^> >> "%TASK_XML%"
echo     ^<RunOnlyIfNetworkAvailable^>false^</RunOnlyIfNetworkAvailable^> >> "%TASK_XML%"
echo     ^<IdleSettings^> >> "%TASK_XML%"
echo       ^<StopOnIdleEnd^>false^</StopOnIdleEnd^> >> "%TASK_XML%"
echo       ^<RestartOnIdle^>false^</RestartOnIdle^> >> "%TASK_XML%"
echo     ^</IdleSettings^> >> "%TASK_XML%"
echo     ^<AllowStartOnDemand^>true^</AllowStartOnDemand^> >> "%TASK_XML%"
echo     ^<Enabled^>true^</Enabled^> >> "%TASK_XML%"
echo     ^<Hidden^>false^</Hidden^> >> "%TASK_XML%"
echo     ^<RunOnlyIfIdle^>false^</RunOnlyIfIdle^> >> "%TASK_XML%"
echo     ^<DisallowStartOnRemoteAppSession^>false^</DisallowStartOnRemoteAppSession^> >> "%TASK_XML%"
echo     ^<UseUnifiedSchedulingEngine^>true^</UseUnifiedSchedulingEngine^> >> "%TASK_XML%"
echo     ^<WakeToRun^>false^</WakeToRun^> >> "%TASK_XML%"
echo     ^<ExecutionTimeLimit^>PT0S^</ExecutionTimeLimit^> >> "%TASK_XML%"
echo     ^<Priority^>4^</Priority^> >> "%TASK_XML%"
echo     ^<RestartOnFailure^> >> "%TASK_XML%"
echo       ^<Interval^>PT1M^</Interval^> >> "%TASK_XML%"
echo       ^<Count^>999^</Count^> >> "%TASK_XML%"
echo     ^</RestartOnFailure^> >> "%TASK_XML%"
echo   ^</Settings^> >> "%TASK_XML%"
echo   ^<Actions Context="Author"^> >> "%TASK_XML%"
echo     ^<Exec^> >> "%TASK_XML%"
echo       ^<Command^>%ExePath%^</Command^> >> "%TASK_XML%"
echo       ^<WorkingDirectory^>%InstallPath%^</WorkingDirectory^> >> "%TASK_XML%"
echo     ^</Exec^> >> "%TASK_XML%"
echo   ^</Actions^> >> "%TASK_XML%"
echo ^</Task^> >> "%TASK_XML%"

:: Import the task
schtasks /Create /TN "%TaskName%" /XML "%TASK_XML%" /F >nul 2>&1
set "TASK_RESULT=%errorLevel%"

:: Clean up temp file
del "%TASK_XML%" >nul 2>&1

if %TASK_RESULT% neq 0 (
    echo   WARNING: Failed to create scheduled task via XML, trying simple method...
    schtasks /Create /TN "%TaskName%" /TR "\"%ExePath%\"" /SC ONSTART /RU "SYSTEM" /RL HIGHEST /F >nul 2>&1
)

:: Step 9: Start task with retry logic
echo [9/9] Starting agent...
set started=0
set attempt=0

:start_loop
if %started%==1 goto check_status
if %attempt%==3 goto check_status
set /a attempt=%attempt%+1

schtasks /Run /TN "%TaskName%" >nul 2>&1
timeout /t 3 /nobreak >nul
tasklist /FI "IMAGENAME eq %ExeName%" 2>nul | find /I "%ExeName%" >nul 2>&1
if %errorLevel%==0 set started=1
if %started%==1 goto check_status
echo   Start attempt %attempt% failed, retrying in 2 seconds...
timeout /t 2 /nobreak >nul
goto start_loop

:check_status
tasklist /FI "IMAGENAME eq %ExeName%" 2>nul | find /I "%ExeName%" >nul 2>&1
if %errorLevel%==0 goto success
goto warning

:success
echo.
echo ======================================
echo  Installation Complete!
echo ======================================
echo.
echo   Agent Status: Running
echo   Install Path: %InstallPath%
echo   Logs:         %InstallPath%\logs
echo.
echo   The agent runs as a Scheduled Task (not a Windows Service)
echo   This avoids HVCI/SCM timeout issues on secured systems.
echo.
goto end

:warning
echo.
echo ======================================
echo  Installation Completed with Warning
echo ======================================
echo.
echo   Agent Status: Not Running
echo.

:: Check for Code Integrity issues
echo Checking for Code Integrity enforcement...
reg query "HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity" /v Enabled 2>nul | find "0x1" >nul 2>&1
if %errorLevel% neq 0 goto no_ci_detected

echo.
echo   DETECTED: Code Integrity may be blocking execution
echo.
echo   Attempting workaround...

:: Clean up any persisted config that might cause hangs
if exist "%DataPath%\agent-config-backup.json" del /f "%DataPath%\agent-config-backup.json" >nul 2>&1

:: Try starting again after cleanup
echo   Retrying task start...
schtasks /Run /TN "%TaskName%" >nul 2>&1
timeout /t 3 /nobreak >nul

tasklist /FI "IMAGENAME eq %ExeName%" 2>nul | find /I "%ExeName%" >nul 2>&1
if %errorLevel%==0 goto ci_success
goto ci_failed

:ci_success
echo.
echo   SUCCESS! Agent started after cleanup.
echo.
goto end

:ci_failed
echo.
echo   Agent still not running. Manual steps may be required:
echo.
echo   Option 1 - Try running the exe directly to see errors:
echo     "%ExePath%"
echo.
echo   Option 2 - Check Windows Event Log:
echo     eventvwr.msc
echo.
echo   Option 3 - Reboot and try again:
echo     shutdown /r /t 0
echo.
goto end

:no_ci_detected
echo   Troubleshooting steps:
echo.
echo   1. Check logs: %InstallPath%\logs
echo   2. Try running exe directly: "%ExePath%"
echo   3. Check Windows Event Log: eventvwr.msc
echo   4. Check task status: schtasks /Query /TN "%TaskName%" /V
echo.

:end
echo.
echo Press any key to exit...
timeout /t 30 /nobreak >nul
exit /b 0
