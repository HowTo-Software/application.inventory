using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text.Json;
using Microsoft.Win32;

namespace ComputerInventory.ThinClient.Services;

/// <summary>
/// Collects EVERYTHING from Windows - no privacy, maximum verbosity!
/// </summary>
[SupportedOSPlatform("windows")]
public class DetailedSystemCollector
{
    private readonly ILogger<DetailedSystemCollector> _logger;

    public DetailedSystemCollector(ILogger<DetailedSystemCollector> logger)
    {
        _logger = logger;
    }

    public async Task<DetailedSystemInfo> CollectAllAsync()
    {
        var info = new DetailedSystemInfo();

        await Task.Run(() =>
        {
            try { CollectBiosInfo(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect BIOS info"); }
            try { CollectMotherboardInfo(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect motherboard info"); }
            try { CollectCpuInfo(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect CPU info"); }
            try { CollectMemoryInfo(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect memory info"); }
            try { CollectStorageInfo(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect storage info"); }
            try { CollectGpuInfo(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect GPU info"); }
            try { CollectMonitorInfo(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect monitor info"); }
            try { CollectNetworkInfo(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect network info"); }
            try { CollectOsInfo(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect OS info"); }
            try { CollectWindowsLicenseInfo(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect license info"); }
            try { CollectInstalledSoftware(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect software info"); }
            try { CollectWindowsUpdates(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect updates info"); }
            try { CollectRunningProcesses(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect processes"); }
            try { CollectServices(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect services"); }
            try { CollectStartupPrograms(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect startup programs"); }
            try { CollectLocalUsers(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect users"); }
            try { CollectSecurityInfo(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect security info"); }
            try { CollectPeripherals(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect peripherals"); }
            try { CollectBatteryInfo(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect battery info"); }
            try { CollectEventLogInfo(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect event log info"); }
            try { CollectPerformanceInfo(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect performance info"); }
            try { CollectChassisInfo(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect chassis info"); }
            try { CollectEnvironmentVariables(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect env vars"); }
            try { CollectScheduledTasks(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect scheduled tasks"); }
            try { CollectSharedFolders(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect shared folders"); }
            try { CollectHardwareIds(info); } catch (Exception ex) { _logger.LogWarning(ex, "Failed to collect hardware IDs"); }
        });

        return info;
    }

    private void CollectBiosInfo(DetailedSystemInfo info)
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
        foreach (var obj in searcher.Get())
        {
            info.Bios.BiosVersion = obj["Version"]?.ToString();
            info.Bios.BiosManufacturer = obj["Manufacturer"]?.ToString();
            info.Bios.BiosSerialNumber = obj["SerialNumber"]?.ToString();
            info.Bios.SmbiosVersion = $"{obj["SMBIOSMajorVersion"]}.{obj["SMBIOSMinorVersion"]}";
            
            var releaseDate = obj["ReleaseDate"]?.ToString();
            if (!string.IsNullOrEmpty(releaseDate))
            {
                try { info.Bios.BiosReleaseDate = ManagementDateTimeConverter.ToDateTime(releaseDate); }
                catch { }
            }
            break;
        }
    }

    private void CollectMotherboardInfo(DetailedSystemInfo info)
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
        foreach (var obj in searcher.Get())
        {
            info.Motherboard.MotherboardManufacturer = obj["Manufacturer"]?.ToString();
            info.Motherboard.MotherboardProduct = obj["Product"]?.ToString();
            info.Motherboard.MotherboardSerialNumber = obj["SerialNumber"]?.ToString();
            info.Motherboard.MotherboardVersion = obj["Version"]?.ToString();
            break;
        }
    }

    private void CollectCpuInfo(DetailedSystemInfo info)
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
        foreach (var obj in searcher.Get())
        {
            info.Cpu.CpuName = obj["Name"]?.ToString()?.Trim();
            info.Cpu.CpuManufacturer = obj["Manufacturer"]?.ToString();
            info.Cpu.CpuCores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
            info.Cpu.CpuLogicalProcessors = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
            info.Cpu.CpuMaxClockSpeed = Convert.ToInt32(obj["MaxClockSpeed"] ?? 0);
            info.Cpu.CpuCurrentClockSpeed = Convert.ToInt32(obj["CurrentClockSpeed"] ?? 0);
            info.Cpu.CpuL2CacheSize = Convert.ToInt32(obj["L2CacheSize"] ?? 0);
            info.Cpu.CpuL3CacheSize = Convert.ToInt32(obj["L3CacheSize"] ?? 0);
            info.Cpu.CpuSocketDesignation = obj["SocketDesignation"]?.ToString();
            info.Cpu.CpuProcessorId = obj["ProcessorId"]?.ToString();
            
            var arch = Convert.ToInt32(obj["Architecture"] ?? 0);
            info.Cpu.CpuArchitecture = arch switch
            {
                0 => "x86",
                5 => "ARM",
                9 => "x64",
                12 => "ARM64",
                _ => $"Unknown ({arch})"
            };
            break;
        }
    }

    private void CollectMemoryInfo(DetailedSystemInfo info)
    {
        // Total memory from ComputerSystem
        using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
        {
            foreach (var obj in searcher.Get())
            {
                info.Memory.TotalPhysicalMemory = Convert.ToInt64(obj["TotalPhysicalMemory"] ?? 0);
                break;
            }
        }

        // Available memory from OS
        using (var searcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory, TotalVirtualMemorySize, FreeVirtualMemory FROM Win32_OperatingSystem"))
        {
            foreach (var obj in searcher.Get())
            {
                info.Memory.AvailablePhysicalMemory = Convert.ToInt64(obj["FreePhysicalMemory"] ?? 0) * 1024;
                info.Memory.TotalVirtualMemory = Convert.ToInt64(obj["TotalVirtualMemorySize"] ?? 0) * 1024;
                info.Memory.AvailableVirtualMemory = Convert.ToInt64(obj["FreeVirtualMemory"] ?? 0) * 1024;
                break;
            }
        }

        // Memory modules
        var modules = new List<MemoryModuleInfo>();
        int slotsUsed = 0;
        int totalSlots = 0;

        using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory"))
        {
            foreach (var obj in searcher.Get())
            {
                slotsUsed++;
                modules.Add(new MemoryModuleInfo
                {
                    Manufacturer = obj["Manufacturer"]?.ToString()?.Trim(),
                    PartNumber = obj["PartNumber"]?.ToString()?.Trim(),
                    Capacity = Convert.ToInt64(obj["Capacity"] ?? 0),
                    Speed = Convert.ToInt32(obj["Speed"] ?? 0),
                    MemoryType = GetMemoryType(Convert.ToInt32(obj["SMBIOSMemoryType"] ?? 0)),
                    FormFactor = GetFormFactor(Convert.ToInt32(obj["FormFactor"] ?? 0)),
                    DeviceLocator = obj["DeviceLocator"]?.ToString(),
                    BankLabel = obj["BankLabel"]?.ToString(),
                    SerialNumber = obj["SerialNumber"]?.ToString()?.Trim()
                });
            }
        }

        // Get total slots
        using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemoryArray"))
        {
            foreach (var obj in searcher.Get())
            {
                totalSlots += Convert.ToInt32(obj["MemoryDevices"] ?? 0);
            }
        }

        info.Memory.MemorySlots = totalSlots;
        info.Memory.MemorySlotsUsed = slotsUsed;
        info.Memory.MemoryModulesJson = JsonSerializer.Serialize(modules);
    }

    private string GetMemoryType(int type) => type switch
    {
        20 => "DDR",
        21 => "DDR2",
        22 => "DDR2 FB-DIMM",
        24 => "DDR3",
        26 => "DDR4",
        34 => "DDR5",
        _ => $"Unknown ({type})"
    };

    private string GetFormFactor(int factor) => factor switch
    {
        8 => "DIMM",
        12 => "SODIMM",
        _ => $"Other ({factor})"
    };

    private void CollectStorageInfo(DetailedSystemInfo info)
    {
        var disks = new List<DiskInfoModel>();
        long totalSize = 0;

        using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
        {
            foreach (var obj in searcher.Get())
            {
                var size = Convert.ToInt64(obj["Size"] ?? 0);
                totalSize += size;
                
                disks.Add(new DiskInfoModel
                {
                    Model = obj["Model"]?.ToString(),
                    SerialNumber = obj["SerialNumber"]?.ToString()?.Trim(),
                    InterfaceType = obj["InterfaceType"]?.ToString(),
                    MediaType = obj["MediaType"]?.ToString(),
                    Size = size,
                    Partitions = Convert.ToInt32(obj["Partitions"] ?? 0),
                    Status = obj["Status"]?.ToString(),
                    FirmwareRevision = obj["FirmwareRevision"]?.ToString()
                });
            }
        }
        info.Storage.DisksJson = JsonSerializer.Serialize(disks);
        info.Storage.TotalDiskSpace = totalSize;

        // Partitions
        var partitions = new List<PartitionInfoModel>();
        using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskPartition"))
        {
            foreach (var obj in searcher.Get())
            {
                partitions.Add(new PartitionInfoModel
                {
                    DiskIndex = Convert.ToInt32(obj["DiskIndex"] ?? 0),
                    PartitionIndex = Convert.ToInt32(obj["Index"] ?? 0),
                    Type = obj["Type"]?.ToString(),
                    Size = Convert.ToInt64(obj["Size"] ?? 0),
                    Bootable = Convert.ToBoolean(obj["Bootable"] ?? false),
                    PrimaryPartition = Convert.ToBoolean(obj["PrimaryPartition"] ?? false)
                });
            }
        }
        info.Storage.PartitionsJson = JsonSerializer.Serialize(partitions);

        // Logical drives
        var logicalDrives = new List<LogicalDriveInfoModel>();
        long freeSpace = 0;

        using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk"))
        {
            foreach (var obj in searcher.Get())
            {
                var driveType = Convert.ToInt32(obj["DriveType"] ?? 0);
                var free = Convert.ToInt64(obj["FreeSpace"] ?? 0);
                
                if (driveType == 3) // Local disk
                    freeSpace += free;

                logicalDrives.Add(new LogicalDriveInfoModel
                {
                    DriveLetter = obj["DeviceID"]?.ToString(),
                    Label = obj["VolumeName"]?.ToString(),
                    FileSystem = obj["FileSystem"]?.ToString(),
                    Size = Convert.ToInt64(obj["Size"] ?? 0),
                    FreeSpace = free,
                    DriveType = driveType switch
                    {
                        2 => "Removable",
                        3 => "Local Disk",
                        4 => "Network",
                        5 => "CD-ROM",
                        _ => "Unknown"
                    }
                });
            }
        }
        info.Storage.LogicalDrivesJson = JsonSerializer.Serialize(logicalDrives);
        info.Storage.FreeDiskSpace = freeSpace;
    }

    private void CollectGpuInfo(DetailedSystemInfo info)
    {
        var gpus = new List<GraphicsCardInfoModel>();

        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
        foreach (var obj in searcher.Get())
        {
            var gpu = new GraphicsCardInfoModel
            {
                Name = obj["Name"]?.ToString(),
                Manufacturer = obj["AdapterCompatibility"]?.ToString(),
                DriverVersion = obj["DriverVersion"]?.ToString(),
                VideoRam = Convert.ToInt64(obj["AdapterRAM"] ?? 0),
                Resolution = $"{obj["CurrentHorizontalResolution"]}x{obj["CurrentVerticalResolution"]}",
                RefreshRate = Convert.ToInt32(obj["CurrentRefreshRate"] ?? 0),
                VideoProcessor = obj["VideoProcessor"]?.ToString()
            };

            var driverDate = obj["DriverDate"]?.ToString();
            if (!string.IsNullOrEmpty(driverDate))
            {
                try { gpu.DriverDate = ManagementDateTimeConverter.ToDateTime(driverDate); }
                catch { }
            }

            gpus.Add(gpu);

            // Set primary GPU info
            if (string.IsNullOrEmpty(info.Gpu.GpuName))
            {
                info.Gpu.GpuName = gpu.Name;
                info.Gpu.GpuManufacturer = gpu.Manufacturer;
                info.Gpu.GpuDriverVersion = gpu.DriverVersion;
                info.Gpu.GpuDriverDate = gpu.DriverDate;
                info.Gpu.GpuRam = gpu.VideoRam;
                info.Gpu.GpuResolution = gpu.Resolution;
            }
        }

        info.Gpu.GraphicsCardsJson = JsonSerializer.Serialize(gpus);
    }

    private void CollectMonitorInfo(DetailedSystemInfo info)
    {
        var monitors = new List<MonitorInfoModel>();

        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DesktopMonitor");
        foreach (var obj in searcher.Get())
        {
            monitors.Add(new MonitorInfoModel
            {
                Name = obj["Name"]?.ToString(),
                Manufacturer = obj["MonitorManufacturer"]?.ToString(),
                ScreenWidth = Convert.ToInt32(obj["ScreenWidth"] ?? 0),
                ScreenHeight = Convert.ToInt32(obj["ScreenHeight"] ?? 0),
                MonitorType = obj["MonitorType"]?.ToString()
            });
        }

        info.Gpu.MonitorsJson = JsonSerializer.Serialize(monitors);
        info.Gpu.MonitorCount = monitors.Count;
    }

    private void CollectNetworkInfo(DetailedSystemInfo info)
    {
        var adapters = new List<NetworkAdapterInfoModel>();

        using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True"))
        {
            foreach (var obj in searcher.Get())
            {
                var ipAddresses = obj["IPAddress"] as string[];
                var subnets = obj["IPSubnet"] as string[];
                var gateways = obj["DefaultIPGateway"] as string[];
                var dnsServers = obj["DNSServerSearchOrder"] as string[];

                adapters.Add(new NetworkAdapterInfoModel
                {
                    Description = obj["Description"]?.ToString(),
                    MacAddress = obj["MACAddress"]?.ToString(),
                    IpAddresses = ipAddresses != null ? string.Join(", ", ipAddresses) : null,
                    SubnetMask = subnets != null ? string.Join(", ", subnets) : null,
                    DefaultGateway = gateways != null ? string.Join(", ", gateways) : null,
                    DnsServers = dnsServers != null ? string.Join(", ", dnsServers) : null,
                    DhcpServer = obj["DHCPServer"]?.ToString(),
                    DhcpEnabled = Convert.ToBoolean(obj["DHCPEnabled"] ?? false)
                });

                // Set primary network info
                if (string.IsNullOrEmpty(info.Network.DefaultGateway) && gateways != null && gateways.Length > 0)
                {
                    info.Network.DefaultGateway = gateways[0];
                    info.Network.DnsServers = dnsServers != null ? string.Join(", ", dnsServers) : null;
                    info.Network.DhcpServer = obj["DHCPServer"]?.ToString();
                }
            }
        }

        // Get adapter speed and status
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            var physicalAddress = nic.GetPhysicalAddress().ToString();
            
            // Only format MAC address if it's the standard 12 characters (6 bytes)
            string? formattedMac = null;
            if (physicalAddress.Length == 12)
            {
                formattedMac = $"{physicalAddress[0..2]}-{physicalAddress[2..4]}-{physicalAddress[4..6]}-{physicalAddress[6..8]}-{physicalAddress[8..10]}-{physicalAddress[10..12]}";
            }
            
            var adapter = adapters.FirstOrDefault(a => 
                !string.IsNullOrEmpty(a.MacAddress) && 
                !string.IsNullOrEmpty(formattedMac) &&
                a.MacAddress.Replace(":", "-").Equals(formattedMac, StringComparison.OrdinalIgnoreCase));
            if (adapter != null)
            {
                adapter.Name = nic.Name;
                adapter.Speed = nic.Speed;
                adapter.Status = nic.OperationalStatus.ToString();
                adapter.ConnectionType = nic.NetworkInterfaceType.ToString();
            }
        }

        info.Network.NetworkAdaptersJson = JsonSerializer.Serialize(adapters);

        // Domain info
        using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
        {
            foreach (var obj in searcher.Get())
            {
                info.Network.DomainName = obj["Domain"]?.ToString();
                info.Network.DnsHostname = obj["DNSHostName"]?.ToString();
                info.Network.Workgroup = obj["Workgroup"]?.ToString();
                info.Network.DomainJoined = Convert.ToBoolean(obj["PartOfDomain"] ?? false);
                break;
            }
        }
    }

    private void CollectOsInfo(DetailedSystemInfo info)
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
        foreach (var obj in searcher.Get())
        {
            info.Os.OsName = obj["Caption"]?.ToString();
            info.Os.OsVersion = obj["Version"]?.ToString();
            info.Os.OsBuild = obj["BuildNumber"]?.ToString();
            info.Os.OsArchitecture = obj["OSArchitecture"]?.ToString();
            info.Os.OsSerialNumber = obj["SerialNumber"]?.ToString();
            info.Os.OsServicePackMajor = Convert.ToInt32(obj["ServicePackMajorVersion"] ?? 0);
            info.Os.OsServicePackMinor = Convert.ToInt32(obj["ServicePackMinorVersion"] ?? 0);
            info.Os.OsLocale = obj["Locale"]?.ToString();
            info.Os.OsLanguage = obj["OSLanguage"]?.ToString();
            info.Os.WindowsRegisteredOwner = obj["RegisteredUser"]?.ToString();
            info.Os.WindowsRegisteredOrganization = obj["Organization"]?.ToString();

            var installDate = obj["InstallDate"]?.ToString();
            if (!string.IsNullOrEmpty(installDate))
                info.Os.OsInstallDate = ManagementDateTimeConverter.ToDateTime(installDate).ToString("yyyy-MM-dd HH:mm:ss");

            var lastBoot = obj["LastBootUpTime"]?.ToString();
            if (!string.IsNullOrEmpty(lastBoot))
            {
                var bootTime = ManagementDateTimeConverter.ToDateTime(lastBoot);
                info.Os.OsLastBootTime = bootTime.ToString("yyyy-MM-dd HH:mm:ss");
                info.Os.OsUptime = (long)(DateTime.Now - bootTime).TotalSeconds;
            }
            break;
        }

        // Time zone
        using (var tzSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_TimeZone"))
        {
            foreach (var obj in tzSearcher.Get())
            {
                info.Os.TimeZone = obj["Caption"]?.ToString();
                break;
            }
        }
    }

    private void CollectWindowsLicenseInfo(DetailedSystemInfo info)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                info.Os.WindowsProductId = key.GetValue("ProductId")?.ToString();
                info.Os.OsProductKey = GetWindowsProductKey();
            }
        }
        catch { }

        // Activation status
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM SoftwareLicensingProduct WHERE ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey IS NOT NULL");
            foreach (var obj in searcher.Get())
            {
                var licenseStatus = Convert.ToInt32(obj["LicenseStatus"] ?? 0);
                info.Os.WindowsIsActivated = licenseStatus == 1;
                info.Os.WindowsActivationStatus = licenseStatus switch
                {
                    0 => "Unlicensed",
                    1 => "Licensed",
                    2 => "OOBGrace",
                    3 => "OOTGrace",
                    4 => "NonGenuineGrace",
                    5 => "Notification",
                    6 => "ExtendedGrace",
                    _ => $"Unknown ({licenseStatus})"
                };
                break;
            }
        }
        catch { }
    }

    private string? GetWindowsProductKey()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var digitalProductId = key?.GetValue("DigitalProductId") as byte[];
            if (digitalProductId == null) return null;

            // Decode product key (simplified - may not work for all versions)
            var keyOffset = 52;
            var isWin8OrUp = (digitalProductId[66] / 6) & 1;
            digitalProductId[66] = (byte)((digitalProductId[66] & 0xF7) | ((isWin8OrUp & 2) * 4));

            const string digits = "BCDFGHJKMPQRTVWXY2346789";
            var key25 = "";
            var last = 0;

            for (var i = 24; i >= 0; i--)
            {
                var current = 0;
                for (var j = 14; j >= 0; j--)
                {
                    current = current * 256 ^ digitalProductId[j + keyOffset];
                    digitalProductId[j + keyOffset] = (byte)(current / 24);
                    current %= 24;
                    last = current;
                }
                key25 = digits[current] + key25;
            }

            if (isWin8OrUp == 1)
            {
                var insert = "N";
                key25 = key25.Insert(1, insert);
                key25 = key25[1..].Insert(last, insert);
            }

            // Format as XXXXX-XXXXX-XXXXX-XXXXX-XXXXX
            return string.Join("-", Enumerable.Range(0, 5).Select(i => key25.Substring(i * 5, 5)));
        }
        catch
        {
            return null;
        }
    }

    private void CollectInstalledSoftware(DetailedSystemInfo info)
    {
        var software = new List<InstalledSoftwareInfoModel>();
        var registryPaths = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var path in registryPaths)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(path);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var name = subKey?.GetValue("DisplayName")?.ToString();
                        if (string.IsNullOrEmpty(name)) continue;

                        // Skip duplicates
                        if (software.Any(s => s.Name == name)) continue;

                        var sw = new InstalledSoftwareInfoModel
                        {
                            Name = name,
                            Version = subKey?.GetValue("DisplayVersion")?.ToString(),
                            Publisher = subKey?.GetValue("Publisher")?.ToString(),
                            InstallLocation = subKey?.GetValue("InstallLocation")?.ToString()
                        };

                        var installDateStr = subKey?.GetValue("InstallDate")?.ToString();
                        if (!string.IsNullOrEmpty(installDateStr) && DateTime.TryParseExact(installDateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var installDate))
                            sw.InstallDate = installDate;

                        var size = subKey?.GetValue("EstimatedSize");
                        if (size != null)
                            sw.Size = Convert.ToInt64(size) * 1024;

                        software.Add(sw);
                    }
                    catch { }
                }
            }
            catch { }
        }

        info.Software.InstalledSoftwareJson = JsonSerializer.Serialize(software.OrderBy(s => s.Name).ToList());
        info.Software.InstalledSoftwareCount = software.Count;
    }

    private void CollectWindowsUpdates(DetailedSystemInfo info)
    {
        var updates = new List<WindowsUpdateInfoModel>();
        DateTime? lastUpdate = null;

        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_QuickFixEngineering");
        foreach (var obj in searcher.Get())
        {
            var update = new WindowsUpdateInfoModel
            {
                HotFixId = obj["HotFixID"]?.ToString(),
                Description = obj["Description"]?.ToString(),
                InstalledBy = obj["InstalledBy"]?.ToString()
            };

            var installedOn = obj["InstalledOn"]?.ToString();
            if (DateTime.TryParse(installedOn, out var date))
            {
                update.InstalledOn = date;
                if (lastUpdate == null || date > lastUpdate)
                    lastUpdate = date;
            }

            updates.Add(update);
        }

        info.Software.InstalledUpdatesJson = JsonSerializer.Serialize(updates.OrderByDescending(u => u.InstalledOn).ToList());
        info.Software.InstalledUpdatesCount = updates.Count;
        info.Software.LastUpdateDate = lastUpdate;
    }

    private void CollectRunningProcesses(DetailedSystemInfo info)
    {
        var processes = new List<ProcessInfoModel>();

        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                var pi = new ProcessInfoModel
                {
                    ProcessId = proc.Id,
                    Name = proc.ProcessName,
                    WorkingSetSize = proc.WorkingSet64
                };

                try { pi.StartTime = proc.StartTime; } catch { }
                try { pi.Path = proc.MainModule?.FileName; } catch { }

                processes.Add(pi);
            }
            catch { }
        }

        info.Software.RunningProcessesJson = JsonSerializer.Serialize(processes.OrderBy(p => p.Name).ToList());
        info.Software.RunningProcessesCount = processes.Count;
    }

    private void CollectServices(DetailedSystemInfo info)
    {
        var services = new List<ServiceInfoModel>();
        int running = 0, stopped = 0;

        foreach (var svc in ServiceController.GetServices())
        {
            try
            {
                var si = new ServiceInfoModel
                {
                    Name = svc.ServiceName,
                    DisplayName = svc.DisplayName,
                    Status = svc.Status.ToString(),
                    StartType = svc.StartType.ToString()
                };

                if (svc.Status == ServiceControllerStatus.Running) running++;
                else if (svc.Status == ServiceControllerStatus.Stopped) stopped++;

                services.Add(si);
            }
            catch { }
        }

        info.Software.ServicesJson = JsonSerializer.Serialize(services.OrderBy(s => s.DisplayName).ToList());
        info.Software.RunningServicesCount = running;
        info.Software.StoppedServicesCount = stopped;
    }

    private void CollectStartupPrograms(DetailedSystemInfo info)
    {
        var startups = new List<StartupProgramInfoModel>();

        // Registry Run keys
        var runKeys = new[]
        {
            (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM"),
            (@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM"),
            (@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM"),
        };

        foreach (var (path, hive) in runKeys)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(path);
                if (key == null) continue;

                foreach (var name in key.GetValueNames())
                {
                    startups.Add(new StartupProgramInfoModel
                    {
                        Name = name,
                        Command = key.GetValue(name)?.ToString(),
                        Location = $"{hive}\\{path}",
                        User = "All Users"
                    });
                }
            }
            catch { }
        }

        // WMI startup
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_StartupCommand");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                if (string.IsNullOrEmpty(name) || startups.Any(s => s.Name == name)) continue;

                startups.Add(new StartupProgramInfoModel
                {
                    Name = name,
                    Command = obj["Command"]?.ToString(),
                    Location = obj["Location"]?.ToString(),
                    User = obj["User"]?.ToString()
                });
            }
        }
        catch { }

        info.Software.StartupProgramsJson = JsonSerializer.Serialize(startups);
        info.Software.StartupProgramsCount = startups.Count;
    }

    private void CollectLocalUsers(DetailedSystemInfo info)
    {
        var users = new List<LocalUserInfoModel>();

        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_UserAccount WHERE LocalAccount=True");
        foreach (var obj in searcher.Get())
        {
            users.Add(new LocalUserInfoModel
            {
                Name = obj["Name"]?.ToString(),
                FullName = obj["FullName"]?.ToString(),
                Description = obj["Description"]?.ToString(),
                Disabled = Convert.ToBoolean(obj["Disabled"] ?? false),
                PasswordRequired = Convert.ToBoolean(obj["PasswordRequired"] ?? false),
                PasswordChangeable = Convert.ToBoolean(obj["PasswordChangeable"] ?? false),
                Sid = obj["SID"]?.ToString()
            });
        }

        info.UsersGroups.LocalUsersJson = JsonSerializer.Serialize(users);

        // Logged on users
        var loggedOn = new List<string>();
        try
        {
            using var searcher2 = new ManagementObjectSearcher("SELECT * FROM Win32_LoggedOnUser");
            foreach (var obj in searcher2.Get())
            {
                var antecedent = obj["Antecedent"]?.ToString();
                if (antecedent != null)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(antecedent, @"Name=""([^""]+)""");
                    if (match.Success && !loggedOn.Contains(match.Groups[1].Value))
                        loggedOn.Add(match.Groups[1].Value);
                }
            }
        }
        catch { }

        info.UsersGroups.LoggedOnUsersJson = JsonSerializer.Serialize(loggedOn);
    }

    private void CollectSecurityInfo(DetailedSystemInfo info)
    {
        // Antivirus - Windows Security Center
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT * FROM AntiVirusProduct");
            foreach (var obj in searcher.Get())
            {
                info.Security.AntivirusProduct = obj["displayName"]?.ToString();
                var state = Convert.ToInt32(obj["productState"] ?? 0);
                info.Security.AntivirusStatus = ((state & 0x1000) != 0) ? "Enabled" : "Disabled";
                break;
            }
        }
        catch { }

        // Firewall
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT * FROM FirewallProduct");
            var hasFirewall = false;
            foreach (var obj in searcher.Get())
            {
                hasFirewall = true;
                break;
            }
            info.Security.FirewallStatus = hasFirewall ? "Third-party" : "Windows Firewall";
        }
        catch 
        {
            info.Security.FirewallStatus = "Unknown";
        }

        // TPM
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\cimv2\Security\MicrosoftTpm", "SELECT * FROM Win32_Tpm");
            foreach (var obj in searcher.Get())
            {
                info.Security.TpmPresent = Convert.ToBoolean(obj["IsActivated_InitialValue"] ?? false) || 
                                   Convert.ToBoolean(obj["IsEnabled_InitialValue"] ?? false);
                info.Security.TpmVersion = obj["SpecVersion"]?.ToString()?.Split(',')[0];
                break;
            }
        }
        catch 
        {
            info.Security.TpmPresent = false;
        }

        // Secure Boot
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            var value = key?.GetValue("UEFISecureBootEnabled");
            info.Security.SecureBootEnabled = value != null && Convert.ToInt32(value) == 1;
        }
        catch
        {
            info.Security.SecureBootEnabled = false;
        }

        // UAC
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            var value = key?.GetValue("EnableLUA");
            info.Security.UacEnabled = value != null && Convert.ToInt32(value) == 1;
        }
        catch
        {
            info.Security.UacEnabled = true; // Default assumption
        }

        // BitLocker (requires admin)
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\cimv2\Security\MicrosoftVolumeEncryption", "SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter='C:'");
            foreach (var obj in searcher.Get())
            {
                var status = Convert.ToInt32(obj["ProtectionStatus"] ?? 0);
                info.Security.BitLockerEnabled = status == 1;
                info.Security.BitLockerStatus = status switch
                {
                    0 => "Off",
                    1 => "On",
                    2 => "Unknown",
                    _ => $"Status {status}"
                };
                break;
            }
        }
        catch
        {
            info.Security.BitLockerEnabled = false;
            info.Security.BitLockerStatus = "Unable to determine";
        }
    }

    private void CollectPeripherals(DetailedSystemInfo info)
    {
        // Printers
        var printers = new List<PrinterInfoModel>();
        using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Printer"))
        {
            foreach (var obj in searcher.Get())
            {
                printers.Add(new PrinterInfoModel
                {
                    Name = obj["Name"]?.ToString(),
                    DriverName = obj["DriverName"]?.ToString(),
                    PortName = obj["PortName"]?.ToString(),
                    Status = obj["PrinterStatus"]?.ToString(),
                    Shared = Convert.ToBoolean(obj["Shared"] ?? false),
                    Default = Convert.ToBoolean(obj["Default"] ?? false),
                    Network = Convert.ToBoolean(obj["Network"] ?? false)
                });
            }
        }
        info.Peripherals.PrintersJson = JsonSerializer.Serialize(printers);

        // USB Devices
        var usb = new List<UsbDeviceInfoModel>();
        using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_USBHub"))
        {
            foreach (var obj in searcher.Get())
            {
                usb.Add(new UsbDeviceInfoModel
                {
                    Name = obj["Name"]?.ToString(),
                    DeviceId = obj["DeviceID"]?.ToString(),
                    Description = obj["Description"]?.ToString(),
                    Status = obj["Status"]?.ToString()
                });
            }
        }
        info.Peripherals.UsbDevicesJson = JsonSerializer.Serialize(usb);

        // Audio devices
        var audio = new List<string>();
        using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SoundDevice"))
        {
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                    audio.Add(name);
            }
        }
        info.Peripherals.AudioDevicesJson = JsonSerializer.Serialize(audio);
    }

    private void CollectBatteryInfo(DetailedSystemInfo info)
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
        foreach (var obj in searcher.Get())
        {
            info.Battery.HasBattery = true;
            info.Battery.BatteryChargePercent = Convert.ToInt32(obj["EstimatedChargeRemaining"] ?? 0);
            
            var status = Convert.ToInt32(obj["BatteryStatus"] ?? 0);
            info.Battery.BatteryStatus = status switch
            {
                1 => "Discharging",
                2 => "AC Connected",
                3 => "Fully Charged",
                4 => "Low",
                5 => "Critical",
                6 => "Charging",
                7 => "Charging High",
                8 => "Charging Low",
                9 => "Charging Critical",
                _ => $"Unknown ({status})"
            };

            info.Battery.BatteryEstimatedRuntime = Convert.ToInt32(obj["EstimatedRunTime"] ?? 0);
            info.Battery.BatteryDesignCapacity = Convert.ToInt32(obj["DesignCapacity"] ?? 0);
            info.Battery.BatteryFullChargeCapacity = Convert.ToInt32(obj["FullChargeCapacity"] ?? 0);
            break;
        }

        if (info.Battery.HasBattery != true)
            info.Battery.HasBattery = false;
    }

    private void CollectEventLogInfo(DetailedSystemInfo info)
    {
        var errors = new List<EventLogEntryModel>();
        var warnings = new List<EventLogEntryModel>();
        int bsodCount = 0;
        DateTime? lastBsod = null;

        try
        {
            using var log = new EventLog("System");
            var entries = log.Entries.Cast<EventLogEntry>()
                .Where(e => e.TimeGenerated > DateTime.Now.AddDays(-30))
                .OrderByDescending(e => e.TimeGenerated)
                .Take(500);

            foreach (var entry in entries)
            {
                if (entry.Source == "BugCheck" || entry.Source == "Microsoft-Windows-WER-SystemErrorReporting")
                {
                    bsodCount++;
                    if (lastBsod == null || entry.TimeGenerated > lastBsod)
                        lastBsod = entry.TimeGenerated;
                }

                if (entry.EntryType == EventLogEntryType.Error && errors.Count < 50)
                {
                    errors.Add(new EventLogEntryModel
                    {
                        TimeGenerated = entry.TimeGenerated,
                        Source = entry.Source,
                        Message = entry.Message?.Length > 500 ? entry.Message[..500] : entry.Message,
                        EventId = (int)entry.InstanceId,
                        EntryType = "Error"
                    });
                }
                else if (entry.EntryType == EventLogEntryType.Warning && warnings.Count < 50)
                {
                    warnings.Add(new EventLogEntryModel
                    {
                        TimeGenerated = entry.TimeGenerated,
                        Source = entry.Source,
                        Message = entry.Message?.Length > 500 ? entry.Message[..500] : entry.Message,
                        EventId = (int)entry.InstanceId,
                        EntryType = "Warning"
                    });
                }
            }
        }
        catch { }

        info.EventLogs.RecentErrorsJson = JsonSerializer.Serialize(errors);
        info.EventLogs.RecentWarningsJson = JsonSerializer.Serialize(warnings);
        info.EventLogs.BlueScreenCount = bsodCount;
        info.EventLogs.LastBlueScreen = lastBsod;
    }

    private void CollectPerformanceInfo(DetailedSystemInfo info)
    {
        try
        {
            using var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            cpuCounter.NextValue();
            System.Threading.Thread.Sleep(100);
            info.Performance.CpuUsagePercent = Math.Round(cpuCounter.NextValue(), 1);
        }
        catch { }

        if (info.Memory.TotalPhysicalMemory > 0 && info.Memory.AvailablePhysicalMemory.HasValue)
        {
            info.Performance.MemoryUsagePercent = Math.Round(100.0 * (1 - (double)info.Memory.AvailablePhysicalMemory / info.Memory.TotalPhysicalMemory.Value), 1);
        }

        if (info.Storage.TotalDiskSpace > 0 && info.Storage.FreeDiskSpace.HasValue)
        {
            info.Performance.DiskUsagePercent = Math.Round(100.0 * (1 - (double)info.Storage.FreeDiskSpace / info.Storage.TotalDiskSpace.Value), 1);
        }
    }

    private void CollectChassisInfo(DetailedSystemInfo info)
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SystemEnclosure");
        foreach (var obj in searcher.Get())
        {
            info.Chassis.ChassisManufacturer = obj["Manufacturer"]?.ToString();
            info.Chassis.ChassisSerialNumber = obj["SerialNumber"]?.ToString();
            info.Chassis.ChassisAssetTag = obj["SMBIOSAssetTag"]?.ToString();
            
            var types = obj["ChassisTypes"] as ushort[];
            if (types != null && types.Length > 0)
            {
                info.Chassis.ChassisType = types[0] switch
                {
                    1 => "Other",
                    2 => "Unknown",
                    3 => "Desktop",
                    4 => "Low Profile Desktop",
                    5 => "Pizza Box",
                    6 => "Mini Tower",
                    7 => "Tower",
                    8 => "Portable",
                    9 => "Laptop",
                    10 => "Notebook",
                    11 => "Hand Held",
                    12 => "Docking Station",
                    13 => "All in One",
                    14 => "Sub Notebook",
                    15 => "Space-Saving",
                    16 => "Lunch Box",
                    17 => "Main System Chassis",
                    18 => "Expansion Chassis",
                    19 => "SubChassis",
                    20 => "Bus Expansion Chassis",
                    21 => "Peripheral Chassis",
                    22 => "Storage Chassis",
                    23 => "Rack Mount Chassis",
                    24 => "Sealed-Case PC",
                    30 => "Tablet",
                    31 => "Convertible",
                    32 => "Detachable",
                    _ => $"Type {types[0]}"
                };
            }
            break;
        }
    }

    private void CollectEnvironmentVariables(DetailedSystemInfo info)
    {
        var envVars = new Dictionary<string, string>();
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine))
        {
            envVars[entry.Key?.ToString() ?? ""] = entry.Value?.ToString() ?? "";
        }
        info.Os.EnvironmentVariablesJson = JsonSerializer.Serialize(envVars);
    }

    private void CollectScheduledTasks(DetailedSystemInfo info)
    {
        var tasks = new List<ScheduledTaskInfoModel>();

        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\TaskScheduler", "SELECT * FROM MSFT_ScheduledTask");
            foreach (var obj in searcher.Get())
            {
                tasks.Add(new ScheduledTaskInfoModel
                {
                    Name = obj["TaskName"]?.ToString(),
                    Path = obj["TaskPath"]?.ToString(),
                    Status = Convert.ToInt32(obj["State"] ?? 0) switch
                    {
                        0 => "Unknown",
                        1 => "Disabled",
                        2 => "Queued",
                        3 => "Ready",
                        4 => "Running",
                        _ => "Other"
                    },
                    Author = obj["Author"]?.ToString()
                });
            }
        }
        catch { }

        info.Software.ScheduledTasksJson = JsonSerializer.Serialize(tasks);
        info.Software.ScheduledTasksCount = tasks.Count;
    }

    private void CollectSharedFolders(DetailedSystemInfo info)
    {
        var shares = new List<SharedFolderInfoModel>();

        using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Share"))
        {
            foreach (var obj in searcher.Get())
            {
                shares.Add(new SharedFolderInfoModel
                {
                    Name = obj["Name"]?.ToString(),
                    Path = obj["Path"]?.ToString(),
                    Description = obj["Description"]?.ToString(),
                    MaximumAllowed = Convert.ToInt32(obj["MaximumAllowed"] ?? 0)
                });
            }
        }

        info.Storage.SharedFoldersJson = JsonSerializer.Serialize(shares);

        // Mapped drives
        var mapped = new List<object>();
        using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_MappedLogicalDisk"))
        {
            foreach (var obj in searcher.Get())
            {
                mapped.Add(new
                {
                    DeviceID = obj["DeviceID"]?.ToString(),
                    ProviderName = obj["ProviderName"]?.ToString()
                });
            }
        }
        info.Storage.MappedDrivesJson = JsonSerializer.Serialize(mapped);
    }

    private void CollectHardwareIds(DetailedSystemInfo info)
    {
        // Machine GUID
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            info.HardwareIds.MachineGuid = key?.GetValue("MachineGuid")?.ToString();
        }
        catch { }

        // Product ID and SKU
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystemProduct");
        foreach (var obj in searcher.Get())
        {
            info.HardwareIds.ProductId = obj["IdentifyingNumber"]?.ToString();
            info.Chassis.SystemSkuNumber = obj["SKUNumber"]?.ToString();
            info.Chassis.SystemFamily = obj["Name"]?.ToString();
            break;
        }
    }
}
