using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text.Json;

namespace ComputerInventory.ThinClient.Services;

/// <summary>
/// Collects comprehensive system data for restore points.
/// Gathers EVERYTHING from Windows for complete system state backup.
/// </summary>
[SupportedOSPlatform("windows")]
public class RestorePointCollector
{
    private readonly ILogger<RestorePointCollector> _logger;
    private readonly DetailedSystemCollector _detailedCollector;
    private readonly JsonSerializerOptions _jsonOptions;

    public RestorePointCollector(ILogger<RestorePointCollector> logger, DetailedSystemCollector detailedCollector)
    {
        _logger = logger;
        _detailedCollector = detailedCollector;
        _jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Collect all data for a restore point
    /// </summary>
    public async Task<RestorePointData> CollectRestorePointDataAsync()
    {
        _logger.LogInformation("Starting restore point data collection...");
        
        var data = new RestorePointData
        {
            CollectedAt = DateTime.UtcNow
        };

        try
        {
            // Collect in parallel where possible
            var tasks = new List<Task>
            {
                Task.Run(() => data.InstalledSoftwareJson = CollectInstalledSoftware()),
                Task.Run(() => data.RunningProcessesJson = CollectRunningProcesses()),
                Task.Run(() => data.WindowsServicesJson = CollectWindowsServices()),
                Task.Run(() => data.StartupProgramsJson = CollectStartupPrograms()),
                Task.Run(() => data.ScheduledTasksJson = CollectScheduledTasks()),
                Task.Run(() => data.EnvironmentVariablesJson = CollectEnvironmentVariables()),
                Task.Run(() => data.NetworkConfigJson = CollectNetworkConfig()),
                Task.Run(() => data.StorageConfigJson = CollectStorageConfig()),
                Task.Run(() => data.EventLogSummaryJson = CollectEventLogSummary()),
                Task.Run(() => data.WindowsUpdatesJson = CollectWindowsUpdates()),
                Task.Run(() => data.DeviceDriversJson = CollectDeviceDrivers()),
                Task.Run(() => data.FirewallRulesJson = CollectFirewallRules()),
                Task.Run(() => data.LocalUsersJson = CollectLocalUsers()),
                Task.Run(() => data.LocalGroupsJson = CollectLocalGroups()),
                Task.Run(() => data.SharedFoldersJson = CollectSharedFolders()),
                Task.Run(() => data.PrintersJson = CollectPrinters()),
                Task.Run(() => data.UsbDevicesJson = CollectUsbDevices()),
                Task.Run(() => data.BatteryStatusJson = CollectBatteryStatus()),
                Task.Run(() => data.BitLockerStatusJson = CollectBitLockerStatus()),
                Task.Run(() => data.SecurityStatusJson = CollectSecurityStatus())
            };

            await Task.WhenAll(tasks);

            // Also get the detailed system info
            data.SystemDetailsJson = JsonSerializer.Serialize(
                await _detailedCollector.CollectAllAsync(), _jsonOptions);

            // Get boot time
            data.SystemBootTime = GetLastBootTime();
            data.UptimeSeconds = GetUptimeSeconds();

            _logger.LogInformation("Restore point data collection complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during restore point data collection");
            data.ErrorMessage = ex.Message;
        }

        return data;
    }

    private DateTime? GetLastBootTime()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var bootTimeStr = obj["LastBootUpTime"]?.ToString();
                if (!string.IsNullOrEmpty(bootTimeStr))
                {
                    return ManagementDateTimeConverter.ToDateTime(bootTimeStr);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get last boot time");
        }
        return null;
    }

    private long? GetUptimeSeconds()
    {
        try
        {
            return (long)TimeSpan.FromMilliseconds(Environment.TickCount64).TotalSeconds;
        }
        catch
        {
            return null;
        }
    }

    private string CollectInstalledSoftware()
    {
        var software = new List<object>();
        try
        {
            // 64-bit software
            CollectSoftwareFromRegistry(software, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            // 32-bit software on 64-bit Windows
            CollectSoftwareFromRegistry(software, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect installed software");
        }
        return JsonSerializer.Serialize(software, _jsonOptions);
    }

    private void CollectSoftwareFromRegistry(List<object> software, string keyPath)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
            if (key == null) return;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                try
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var displayName = subKey.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrEmpty(displayName)) continue;

                    software.Add(new
                    {
                        Name = displayName,
                        Version = subKey.GetValue("DisplayVersion")?.ToString(),
                        Publisher = subKey.GetValue("Publisher")?.ToString(),
                        InstallDate = subKey.GetValue("InstallDate")?.ToString(),
                        InstallLocation = subKey.GetValue("InstallLocation")?.ToString(),
                        UninstallString = subKey.GetValue("UninstallString")?.ToString(),
                        EstimatedSizeKB = subKey.GetValue("EstimatedSize"),
                    });
                }
                catch { }
            }
        }
        catch { }
    }

    private string CollectRunningProcesses()
    {
        var processes = new List<object>();
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    processes.Add(new
                    {
                        proc.Id,
                        proc.ProcessName,
                        MemoryMB = proc.WorkingSet64 / 1024.0 / 1024.0,
                        StartTime = TryGetProcessStartTime(proc),
                        proc.Responding,
                        ThreadCount = proc.Threads.Count,
                        MainWindowTitle = TryGetMainWindowTitle(proc),
                        FileName = TryGetFileName(proc)
                    });
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect running processes");
        }
        return JsonSerializer.Serialize(processes, _jsonOptions);
    }

    private DateTime? TryGetProcessStartTime(Process proc)
    {
        try { return proc.StartTime; } catch { return null; }
    }

    private string? TryGetMainWindowTitle(Process proc)
    {
        try { return string.IsNullOrEmpty(proc.MainWindowTitle) ? null : proc.MainWindowTitle; } catch { return null; }
    }

    private string? TryGetFileName(Process proc)
    {
        try { return proc.MainModule?.FileName; } catch { return null; }
    }

    private string CollectWindowsServices()
    {
        var services = new List<object>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Service");
            foreach (ManagementObject obj in searcher.Get())
            {
                services.Add(new
                {
                    Name = obj["Name"]?.ToString(),
                    DisplayName = obj["DisplayName"]?.ToString(),
                    State = obj["State"]?.ToString(),
                    StartMode = obj["StartMode"]?.ToString(),
                    PathName = obj["PathName"]?.ToString(),
                    StartName = obj["StartName"]?.ToString(),  // Account running as
                    Description = obj["Description"]?.ToString(),
                    ProcessId = obj["ProcessId"]
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect Windows services");
        }
        return JsonSerializer.Serialize(services, _jsonOptions);
    }

    private string CollectStartupPrograms()
    {
        var startups = new List<object>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_StartupCommand");
            foreach (ManagementObject obj in searcher.Get())
            {
                startups.Add(new
                {
                    Name = obj["Name"]?.ToString(),
                    Command = obj["Command"]?.ToString(),
                    Location = obj["Location"]?.ToString(),
                    User = obj["User"]?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect startup programs");
        }
        return JsonSerializer.Serialize(startups, _jsonOptions);
    }

    private string CollectScheduledTasks()
    {
        var tasks = new List<object>();
        try
        {
            // Use schtasks command to get scheduled tasks
            var startInfo = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = "/query /fo csv /v",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Use HVCI-safe process launch with timeout
            using var process = HvciSafeProcess.StartWithTimeoutAsync(startInfo, TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                var exited = HvciSafeProcess.WaitForExitAsync(process, TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                if (!exited)
                {
                    try { process.Kill(); } catch { }
                }

                // Parse CSV output
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 1)
                {
                    var headers = ParseCsvLine(lines[0]);
                    for (int i = 1; i < Math.Min(lines.Length, 500); i++) // Limit to 500 tasks
                    {
                        try
                        {
                            var values = ParseCsvLine(lines[i]);
                            if (values.Length >= 5)
                            {
                                tasks.Add(new
                                {
                                    TaskName = values.Length > 0 ? values[0] : null,
                                    NextRunTime = values.Length > 1 ? values[1] : null,
                                    Status = values.Length > 2 ? values[2] : null,
                                    LastRunTime = values.Length > 4 ? values[4] : null,
                                    Author = values.Length > 7 ? values[7] : null,
                                    TaskToRun = values.Length > 8 ? values[8] : null
                                });
                            }
                        }
                        catch { }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect scheduled tasks");
        }
        return JsonSerializer.Serialize(tasks, _jsonOptions);
    }

    private string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = "";
        var inQuotes = false;
        
        foreach (var c in line)
        {
            if (c == '"')
                inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.Trim());
                current = "";
            }
            else
                current += c;
        }
        result.Add(current.Trim());
        return result.ToArray();
    }

    private string CollectEnvironmentVariables()
    {
        var envVars = new Dictionary<string, string>();
        try
        {
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                envVars[entry.Key?.ToString() ?? ""] = entry.Value?.ToString() ?? "";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect environment variables");
        }
        return JsonSerializer.Serialize(envVars, _jsonOptions);
    }

    private string CollectNetworkConfig()
    {
        var config = new Dictionary<string, object>();
        try
        {
            // Network adapters
            var adapters = new List<object>();
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True");
            foreach (ManagementObject obj in searcher.Get())
            {
                adapters.Add(new
                {
                    Description = obj["Description"]?.ToString(),
                    MACAddress = obj["MACAddress"]?.ToString(),
                    IPAddress = obj["IPAddress"],
                    IPSubnet = obj["IPSubnet"],
                    DefaultIPGateway = obj["DefaultIPGateway"],
                    DNSServerSearchOrder = obj["DNSServerSearchOrder"],
                    DHCPEnabled = obj["DHCPEnabled"],
                    DHCPServer = obj["DHCPServer"]?.ToString(),
                    DNSDomain = obj["DNSDomain"]?.ToString()
                });
            }
            config["Adapters"] = adapters;

            // Hosts file
            try
            {
                var hostsPath = Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts");
                if (File.Exists(hostsPath))
                {
                    config["HostsFile"] = File.ReadAllText(hostsPath);
                }
            }
            catch { }

            // DNS cache (limited)
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ipconfig",
                    Arguments = "/displaydns",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                // Use HVCI-safe process launch
                using var process = HvciSafeProcess.StartWithTimeoutAsync(startInfo, TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    config["DnsCache"] = output.Substring(0, Math.Min(10000, output.Length));
                    HvciSafeProcess.WaitForExitAsync(process, TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect network config");
        }
        return JsonSerializer.Serialize(config, _jsonOptions);
    }

    private string CollectStorageConfig()
    {
        var storage = new Dictionary<string, object>();
        try
        {
            // Physical disks
            var disks = new List<object>();
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    disks.Add(new
                    {
                        Model = obj["Model"]?.ToString(),
                        SerialNumber = obj["SerialNumber"]?.ToString()?.Trim(),
                        Size = obj["Size"],
                        InterfaceType = obj["InterfaceType"]?.ToString(),
                        MediaType = obj["MediaType"]?.ToString(),
                        Partitions = obj["Partitions"]
                    });
                }
            }
            storage["PhysicalDisks"] = disks;

            // Logical drives
            var drives = new List<object>();
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    drives.Add(new
                    {
                        DeviceID = obj["DeviceID"]?.ToString(),
                        DriveType = obj["DriveType"],
                        FileSystem = obj["FileSystem"]?.ToString(),
                        Size = obj["Size"],
                        FreeSpace = obj["FreeSpace"],
                        VolumeName = obj["VolumeName"]?.ToString(),
                        VolumeSerialNumber = obj["VolumeSerialNumber"]?.ToString()
                    });
                }
            }
            storage["LogicalDrives"] = drives;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect storage config");
        }
        return JsonSerializer.Serialize(storage, _jsonOptions);
    }

    private string CollectEventLogSummary()
    {
        var summary = new Dictionary<string, object>();
        try
        {
            var logs = new[] { "System", "Application", "Security" };
            foreach (var logName in logs)
            {
                try
                {
                    var events = new List<object>();
                    using var eventLog = new EventLog(logName);
                    var entries = eventLog.Entries.Cast<EventLogEntry>()
                        .Where(e => e.TimeGenerated > DateTime.Now.AddDays(-7))
                        .Where(e => e.EntryType == EventLogEntryType.Error || e.EntryType == EventLogEntryType.Warning)
                        .OrderByDescending(e => e.TimeGenerated)
                        .Take(100);

                    foreach (var entry in entries)
                    {
                        events.Add(new
                        {
                            entry.TimeGenerated,
                            entry.Source,
                            entry.EntryType,
                            EventId = entry.InstanceId,
                            Message = entry.Message?.Substring(0, Math.Min(500, entry.Message?.Length ?? 0))
                        });
                    }
                    summary[logName] = events;
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect event log summary");
        }
        return JsonSerializer.Serialize(summary, _jsonOptions);
    }

    private string CollectWindowsUpdates()
    {
        var updates = new List<object>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_QuickFixEngineering");
            foreach (ManagementObject obj in searcher.Get())
            {
                updates.Add(new
                {
                    HotFixID = obj["HotFixID"]?.ToString(),
                    Description = obj["Description"]?.ToString(),
                    InstalledOn = obj["InstalledOn"]?.ToString(),
                    InstalledBy = obj["InstalledBy"]?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect Windows updates");
        }
        return JsonSerializer.Serialize(updates, _jsonOptions);
    }

    private string CollectDeviceDrivers()
    {
        var drivers = new List<object>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPSignedDriver");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["DeviceName"]?.ToString();
                if (string.IsNullOrEmpty(name)) continue;

                drivers.Add(new
                {
                    DeviceName = name,
                    DriverVersion = obj["DriverVersion"]?.ToString(),
                    Manufacturer = obj["Manufacturer"]?.ToString(),
                    DriverDate = obj["DriverDate"]?.ToString(),
                    IsSigned = obj["IsSigned"],
                    DeviceClass = obj["DeviceClass"]?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect device drivers");
        }
        return JsonSerializer.Serialize(drivers, _jsonOptions);
    }

    private string CollectFirewallRules()
    {
        var rules = new List<object>();
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "advfirewall firewall show rule name=all",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Use HVCI-safe process launch with timeout
            using var process = HvciSafeProcess.StartWithTimeoutAsync(startInfo, TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                var exited = HvciSafeProcess.WaitForExitAsync(process, TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                if (!exited)
                {
                    try { process.Kill(); } catch { }
                }

                // Parse rules (simplified)
                var ruleBlocks = output.Split(new[] { "Rule Name:" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var block in ruleBlocks.Take(200)) // Limit to 200 rules
                {
                    var lines = block.Split('\n');
                    var rule = new Dictionary<string, string>();
                    
                    if (lines.Length > 0)
                        rule["Name"] = lines[0].Trim();

                    foreach (var line in lines.Skip(1))
                    {
                        var parts = line.Split(new[] { ':' }, 2);
                        if (parts.Length == 2)
                        {
                            rule[parts[0].Trim()] = parts[1].Trim();
                        }
                    }

                    if (rule.Count > 1)
                        rules.Add(rule);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect firewall rules");
        }
        return JsonSerializer.Serialize(rules, _jsonOptions);
    }

    private string CollectLocalUsers()
    {
        var users = new List<object>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_UserAccount WHERE LocalAccount = True");
            foreach (ManagementObject obj in searcher.Get())
            {
                users.Add(new
                {
                    Name = obj["Name"]?.ToString(),
                    FullName = obj["FullName"]?.ToString(),
                    Description = obj["Description"]?.ToString(),
                    SID = obj["SID"]?.ToString(),
                    Disabled = obj["Disabled"],
                    Lockout = obj["Lockout"],
                    PasswordRequired = obj["PasswordRequired"],
                    PasswordChangeable = obj["PasswordChangeable"]
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect local users");
        }
        return JsonSerializer.Serialize(users, _jsonOptions);
    }

    private string CollectLocalGroups()
    {
        var groups = new List<object>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Group WHERE LocalAccount = True");
            foreach (ManagementObject obj in searcher.Get())
            {
                var groupName = obj["Name"]?.ToString();
                var members = new List<string>();

                // Get group members
                try
                {
                    using var memberSearcher = new ManagementObjectSearcher(
                        $"SELECT * FROM Win32_GroupUser WHERE GroupComponent = \"Win32_Group.Domain='{Environment.MachineName}',Name='{groupName}'\"");
                    foreach (ManagementObject member in memberSearcher.Get())
                    {
                        var partComponent = member["PartComponent"]?.ToString();
                        if (!string.IsNullOrEmpty(partComponent))
                        {
                            var nameMatch = System.Text.RegularExpressions.Regex.Match(partComponent, @"Name=""([^""]+)""");
                            if (nameMatch.Success)
                                members.Add(nameMatch.Groups[1].Value);
                        }
                    }
                }
                catch { }

                groups.Add(new
                {
                    Name = groupName,
                    Description = obj["Description"]?.ToString(),
                    SID = obj["SID"]?.ToString(),
                    Members = members
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect local groups");
        }
        return JsonSerializer.Serialize(groups, _jsonOptions);
    }

    private string CollectSharedFolders()
    {
        var shares = new List<object>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Share");
            foreach (ManagementObject obj in searcher.Get())
            {
                shares.Add(new
                {
                    Name = obj["Name"]?.ToString(),
                    Path = obj["Path"]?.ToString(),
                    Description = obj["Description"]?.ToString(),
                    Status = obj["Status"]?.ToString(),
                    Type = obj["Type"]
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect shared folders");
        }
        return JsonSerializer.Serialize(shares, _jsonOptions);
    }

    private string CollectPrinters()
    {
        var printers = new List<object>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Printer");
            foreach (ManagementObject obj in searcher.Get())
            {
                printers.Add(new
                {
                    Name = obj["Name"]?.ToString(),
                    DriverName = obj["DriverName"]?.ToString(),
                    PortName = obj["PortName"]?.ToString(),
                    Default = obj["Default"],
                    Network = obj["Network"],
                    Shared = obj["Shared"],
                    ShareName = obj["ShareName"]?.ToString(),
                    PrinterStatus = obj["PrinterStatus"],
                    Location = obj["Location"]?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect printers");
        }
        return JsonSerializer.Serialize(printers, _jsonOptions);
    }

    private string CollectUsbDevices()
    {
        var devices = new List<object>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_USBControllerDevice");
            foreach (ManagementObject obj in searcher.Get())
            {
                var dependent = obj["Dependent"]?.ToString();
                if (!string.IsNullOrEmpty(dependent))
                {
                    // Extract device ID
                    var match = System.Text.RegularExpressions.Regex.Match(dependent, @"DeviceID=""([^""]+)""");
                    if (match.Success)
                    {
                        var deviceId = match.Groups[1].Value.Replace("\\\\", "\\");
                        
                        // Get device details
                        try
                        {
                            using var deviceSearcher = new ManagementObjectSearcher(
                                $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{deviceId.Replace("\\", "\\\\")}'");
                            foreach (ManagementObject device in deviceSearcher.Get())
                            {
                                devices.Add(new
                                {
                                    DeviceID = deviceId,
                                    Name = device["Name"]?.ToString(),
                                    Description = device["Description"]?.ToString(),
                                    Manufacturer = device["Manufacturer"]?.ToString(),
                                    Status = device["Status"]?.ToString()
                                });
                            }
                        }
                        catch { }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect USB devices");
        }
        return JsonSerializer.Serialize(devices.DistinctBy(d => ((dynamic)d).DeviceID).ToList(), _jsonOptions);
    }

    private string CollectBatteryStatus()
    {
        var battery = new Dictionary<string, object>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
            foreach (ManagementObject obj in searcher.Get())
            {
                battery["Name"] = obj["Name"]?.ToString() ?? "Unknown";
                battery["Status"] = obj["Status"]?.ToString() ?? "Unknown";
                battery["EstimatedChargeRemaining"] = obj["EstimatedChargeRemaining"];
                battery["EstimatedRunTime"] = obj["EstimatedRunTime"];
                battery["BatteryStatus"] = obj["BatteryStatus"];
                battery["Chemistry"] = obj["Chemistry"];
                battery["DesignCapacity"] = obj["DesignCapacity"];
                battery["FullChargeCapacity"] = obj["FullChargeCapacity"];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect battery status");
        }
        return JsonSerializer.Serialize(battery, _jsonOptions);
    }

    private string CollectBitLockerStatus()
    {
        var bitlocker = new List<object>();
        try
        {
            // BitLocker requires elevated permissions
            using var searcher = new ManagementObjectSearcher(
                new ManagementScope(@"\\.\root\CIMV2\Security\MicrosoftVolumeEncryption"),
                new ObjectQuery("SELECT * FROM Win32_EncryptableVolume"));
            
            foreach (ManagementObject obj in searcher.Get())
            {
                bitlocker.Add(new
                {
                    DriveLetter = obj["DriveLetter"]?.ToString(),
                    ProtectionStatus = obj["ProtectionStatus"],
                    ConversionStatus = obj["ConversionStatus"],
                    EncryptionMethod = obj["EncryptionMethod"]
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect BitLocker status (may require admin)");
        }
        return JsonSerializer.Serialize(bitlocker, _jsonOptions);
    }

    private string CollectSecurityStatus()
    {
        var security = new Dictionary<string, object>();
        try
        {
            // Windows Security Center (Antivirus, Firewall, etc.)
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    new ManagementScope(@"\\.\root\SecurityCenter2"),
                    new ObjectQuery("SELECT * FROM AntiVirusProduct"));
                
                var antiviruses = new List<object>();
                foreach (ManagementObject obj in searcher.Get())
                {
                    antiviruses.Add(new
                    {
                        DisplayName = obj["displayName"]?.ToString(),
                        InstanceGuid = obj["instanceGuid"]?.ToString(),
                        ProductState = obj["productState"]
                    });
                }
                security["AntiVirus"] = antiviruses;
            }
            catch { }

            // Firewall products
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    new ManagementScope(@"\\.\root\SecurityCenter2"),
                    new ObjectQuery("SELECT * FROM FirewallProduct"));
                
                var firewalls = new List<object>();
                foreach (ManagementObject obj in searcher.Get())
                {
                    firewalls.Add(new
                    {
                        DisplayName = obj["displayName"]?.ToString(),
                        InstanceGuid = obj["instanceGuid"]?.ToString(),
                        ProductState = obj["productState"]
                    });
                }
                security["Firewall"] = firewalls;
            }
            catch { }

            // UAC status
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
                if (key != null)
                {
                    security["UAC"] = new
                    {
                        EnableLUA = key.GetValue("EnableLUA"),
                        ConsentPromptBehaviorAdmin = key.GetValue("ConsentPromptBehaviorAdmin"),
                        ConsentPromptBehaviorUser = key.GetValue("ConsentPromptBehaviorUser")
                    };
                }
            }
            catch { }

            // Windows Defender status
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection");
                if (key != null)
                {
                    security["WindowsDefender"] = new
                    {
                        DisableRealtimeMonitoring = key.GetValue("DisableRealtimeMonitoring"),
                        DisableBehaviorMonitoring = key.GetValue("DisableBehaviorMonitoring"),
                        DisableOnAccessProtection = key.GetValue("DisableOnAccessProtection")
                    };
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect security status");
        }
        return JsonSerializer.Serialize(security, _jsonOptions);
    }
}

/// <summary>
/// Data collected for a restore point
/// </summary>
public class RestorePointData
{
    public DateTime CollectedAt { get; set; }
    public DateTime? SystemBootTime { get; set; }
    public long? UptimeSeconds { get; set; }
    
    public string? SystemDetailsJson { get; set; }
    public string? InstalledSoftwareJson { get; set; }
    public string? RunningProcessesJson { get; set; }
    public string? WindowsServicesJson { get; set; }
    public string? StartupProgramsJson { get; set; }
    public string? ScheduledTasksJson { get; set; }
    public string? EnvironmentVariablesJson { get; set; }
    public string? NetworkConfigJson { get; set; }
    public string? StorageConfigJson { get; set; }
    public string? EventLogSummaryJson { get; set; }
    public string? WindowsUpdatesJson { get; set; }
    public string? DeviceDriversJson { get; set; }
    public string? FirewallRulesJson { get; set; }
    public string? LocalUsersJson { get; set; }
    public string? LocalGroupsJson { get; set; }
    public string? SharedFoldersJson { get; set; }
    public string? PrintersJson { get; set; }
    public string? UsbDevicesJson { get; set; }
    public string? BatteryStatusJson { get; set; }
    public string? BitLockerStatusJson { get; set; }
    public string? SecurityStatusJson { get; set; }
    
    public string? ErrorMessage { get; set; }
}
