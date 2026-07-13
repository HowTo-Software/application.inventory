using System.Management;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;
using ComputerInventory.ThinClient.Models;

namespace ComputerInventory.ThinClient.Services;

public interface ISystemInfoService
{
    Task<SystemInfo> GetSystemInfoAsync();
    Task<string> GetSerialNumberAsync();
    Task<bool> DetectHardwareChangesAsync();
    Task<string?> GetPublicIpAddressAsync();
    Task<GpsLocation?> GetGpsLocationAsync();
}

[SupportedOSPlatform("windows")]
public class SystemInfoService : ISystemInfoService
{
    private readonly ILogger<SystemInfoService> _logger;
    private readonly HttpClient _httpClient;
    private readonly ILocationService _locationService;
    private SystemInfo? _lastKnownInfo;
    private string? _cachedPublicIp;
    private DateTime _publicIpCacheTime = DateTime.MinValue;
    private static readonly TimeSpan PublicIpCacheDuration = TimeSpan.FromHours(1);

    public SystemInfoService(ILogger<SystemInfoService> logger, IHttpClientFactory httpClientFactory, ILocationService locationService)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("PublicIp");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _locationService = locationService;
    }

    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        var systemInfo = await Task.Run(() =>
        {
            var info = new SystemInfo
            {
                Hostname = Environment.MachineName,
                OperatingSystem = GetOperatingSystem(),
                OsVersion = Environment.OSVersion.VersionString,
                CurrentUser = GetCurrentUser(),
                LastBootTime = GetLastBootTime(),
                IsOnline = true
            };

            // Get BIOS/System information
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
                foreach (var obj in searcher.Get())
                {
                    info.SerialNumber = obj["SerialNumber"]?.ToString() ?? "Unknown";
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get BIOS serial number");
                info.SerialNumber = Environment.MachineName; // Fallback
            }

            // Get Computer System information
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get())
                {
                    info.Manufacturer = obj["Manufacturer"]?.ToString() ?? "Unknown";
                    info.Model = obj["Model"]?.ToString() ?? "Unknown";
                    var totalMemory = Convert.ToUInt64(obj["TotalPhysicalMemory"] ?? 0);
                    info.RamGB = (int)(totalMemory / 1024 / 1024 / 1024);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get computer system info");
            }

            // Get Processor information
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    info.Processor = obj["Name"]?.ToString();
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get processor info");
            }

            // Get Disk information
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive WHERE MediaType='Fixed hard disk media'");
                long totalSize = 0;
                foreach (var obj in searcher.Get())
                {
                    totalSize += Convert.ToInt64(obj["Size"] ?? 0);
                }
                info.StorageGB = (int)(totalSize / 1024 / 1024 / 1024);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get disk info");
            }

            // Get Network information
            info.IpAddress = GetLocalIpAddress();
            info.MacAddress = GetMacAddress();

            // Get Chassis/Computer Type
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure");
                foreach (var obj in searcher.Get())
                {
                    var types = obj["ChassisTypes"] as ushort[];
                    if (types != null && types.Length > 0)
                    {
                        info.ComputerType = types[0] switch
                        {
                            3 or 4 or 5 or 6 or 7 or 15 or 16 or 24 => "Desktop",  // Desktop types
                            8 or 9 or 10 or 14 => "Laptop",                         // Portable/Laptop types
                            30 or 31 or 32 => "Tablet",                             // Tablet types
                            17 or 18 or 19 or 20 or 21 or 22 or 23 => "Server",    // Server/Rack types
                            11 => "Handheld",
                            12 => "DockingStation",
                            13 => "AllInOne",
                            _ => "Desktop"  // Default to Desktop for unknown types
                        };
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get chassis type, defaulting to Desktop");
                info.ComputerType = "Desktop";
            }

            _lastKnownInfo = info;
            return info;
        });
        
        // Get public IP asynchronously (with caching) - outside Task.Run for async call
        systemInfo.PublicIpAddress = await GetPublicIpAddressAsync();
        
        // Get GPS location (with caching) - runs in parallel or after public IP
        var gpsLocation = await GetGpsLocationAsync();
        if (gpsLocation != null)
        {
            systemInfo.GpsLatitude = gpsLocation.Latitude;
            systemInfo.GpsLongitude = gpsLocation.Longitude;
            _logger.LogDebug("GPS location added to system info: Lat={Lat}, Lon={Lon}", 
                gpsLocation.Latitude, gpsLocation.Longitude);
        }
        
        return systemInfo;
    }

    /// <summary>
    /// Gets the current GPS location using Windows Location Services.
    /// </summary>
    public async Task<GpsLocation?> GetGpsLocationAsync()
    {
        try
        {
            return await _locationService.GetGpsLocationAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get GPS location");
            return null;
        }
    }

    public async Task<string> GetSerialNumberAsync()
    {
        if (_lastKnownInfo != null)
        {
            return _lastKnownInfo.SerialNumber;
        }

        var info = await GetSystemInfoAsync();
        return info.SerialNumber;
    }

    public async Task<bool> DetectHardwareChangesAsync()
    {
        if (_lastKnownInfo == null)
        {
            return false;
        }

        var currentInfo = await GetSystemInfoAsync();
        
        // Check for significant changes
        var hasChanges = 
            currentInfo.RamGB != _lastKnownInfo.RamGB ||
            currentInfo.StorageGB != _lastKnownInfo.StorageGB ||
            currentInfo.IpAddress != _lastKnownInfo.IpAddress ||
            currentInfo.MacAddress != _lastKnownInfo.MacAddress;

        return hasChanges;
    }

    private static string GetOperatingSystem()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                return obj["Caption"]?.ToString() ?? "Windows";
            }
        }
        catch { }
        return $"Windows {Environment.OSVersion.Version}";
    }

    /// <summary>
    /// Gets the interactive user with the longest session time on this machine.
    /// Filters out system accounts (those ending with $, SYSTEM, LOCAL SERVICE, NETWORK SERVICE).
    /// </summary>
    private static string? GetCurrentUser()
    {
        try
        {
            var userSessions = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
            
            // Query active logon sessions - Interactive sessions (type 2) and Remote Interactive (type 10)
            using var sessionSearcher = new ManagementObjectSearcher(
                "SELECT LogonId, StartTime, LogonType FROM Win32_LogonSession WHERE LogonType = 2 OR LogonType = 10");
            
            foreach (ManagementObject session in sessionSearcher.Get())
            {
                var logonId = session["LogonId"]?.ToString();
                var startTimeStr = session["StartTime"]?.ToString();
                
                if (string.IsNullOrEmpty(logonId) || string.IsNullOrEmpty(startTimeStr))
                    continue;
                
                DateTime startTime;
                try
                {
                    startTime = ManagementDateTimeConverter.ToDateTime(startTimeStr);
                }
                catch
                {
                    continue;
                }
                
                // Find the user associated with this session
                using var userSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_LogonSession.LogonId='{logonId}'}} WHERE AssocClass=Win32_LoggedOnUser Role=Dependent");
                
                foreach (ManagementObject user in userSearcher.Get())
                {
                    var userName = user["Name"]?.ToString();
                    var domain = user["Domain"]?.ToString();
                    
                    if (string.IsNullOrEmpty(userName))
                        continue;
                    
                    // Skip system/machine accounts
                    if (userName.EndsWith("$") ||
                        userName.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
                        userName.Equals("LOCAL SERVICE", StringComparison.OrdinalIgnoreCase) ||
                        userName.Equals("NETWORK SERVICE", StringComparison.OrdinalIgnoreCase) ||
                        userName.Equals("DWM-1", StringComparison.OrdinalIgnoreCase) ||
                        userName.Equals("DWM-2", StringComparison.OrdinalIgnoreCase) ||
                        userName.Equals("DWM-3", StringComparison.OrdinalIgnoreCase) ||
                        userName.Equals("UMFD-0", StringComparison.OrdinalIgnoreCase) ||
                        userName.Equals("UMFD-1", StringComparison.OrdinalIgnoreCase) ||
                        userName.Equals("UMFD-2", StringComparison.OrdinalIgnoreCase) ||
                        userName.Equals("UMFD-3", StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    // Calculate session duration
                    var sessionDuration = DateTime.Now - startTime;
                    if (sessionDuration < TimeSpan.Zero)
                        sessionDuration = TimeSpan.Zero;
                    
                    // Add or update user's total session time
                    if (userSessions.TryGetValue(userName, out var existingDuration))
                    {
                        // Keep the longer session if same user has multiple sessions
                        if (sessionDuration > existingDuration)
                        {
                            userSessions[userName] = sessionDuration;
                        }
                    }
                    else
                    {
                        userSessions[userName] = sessionDuration;
                    }
                    break; // Only need one user per session
                }
            }
            
            // Return the user with the longest session time
            if (userSessions.Count > 0)
            {
                var topUser = userSessions.OrderByDescending(kvp => kvp.Value).First();
                return topUser.Key;
            }
            
            // Fallback: try to get the owner of explorer.exe (the logged-in user's shell)
            return GetExplorerOwner();
        }
        catch
        {
            // Final fallback to Environment.UserName (may return service account)
            try
            {
                var fallbackUser = Environment.UserName;
                // Don't return machine account
                if (!string.IsNullOrEmpty(fallbackUser) && !fallbackUser.EndsWith("$"))
                {
                    return fallbackUser;
                }
            }
            catch { }
            return null;
        }
    }
    
    /// <summary>
    /// Gets the owner of explorer.exe process as a fallback method to identify logged-in user.
    /// </summary>
    private static string? GetExplorerOwner()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_Process WHERE Name = 'explorer.exe'");
            
            foreach (ManagementObject process in searcher.Get())
            {
                var outParams = new object[2];
                var result = (uint)process.InvokeMethod("GetOwner", outParams);
                
                if (result == 0)
                {
                    var userName = outParams[0]?.ToString();
                    if (!string.IsNullOrEmpty(userName) && !userName.EndsWith("$"))
                    {
                        return userName;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static DateTime GetLastBootTime()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                var lastBoot = obj["LastBootUpTime"]?.ToString();
                if (!string.IsNullOrEmpty(lastBoot))
                {
                    return ManagementDateTimeConverter.ToDateTime(lastBoot);
                }
            }
        }
        catch { }
        return DateTime.Now.AddHours(-1);
    }

    private static string? GetLocalIpAddress()
    {
        try
        {
            // Get the primary network adapter (the one with a default gateway)
            var primaryAdapter = GetPrimaryNetworkAdapter();
            if (primaryAdapter != null)
            {
                var ipProps = primaryAdapter.GetIPProperties();
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return addr.Address.ToString();
                    }
                }
            }

            // Fallback: iterate through all adapters
            foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (netInterface.OperationalStatus != OperationalStatus.Up) continue;
                if (netInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var ipProps = netInterface.GetIPProperties();
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return addr.Address.ToString();
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static string? GetMacAddress()
    {
        try
        {
            // Get the primary network adapter (the one with a default gateway)
            var primaryAdapter = GetPrimaryNetworkAdapter();
            if (primaryAdapter != null)
            {
                var mac = primaryAdapter.GetPhysicalAddress().ToString();
                if (!string.IsNullOrEmpty(mac) && mac != "000000000000" && mac.Length == 12)
                {
                    return string.Join(":", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)));
                }
            }

            // Fallback: iterate through all adapters
            foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (netInterface.OperationalStatus != OperationalStatus.Up) continue;
                if (netInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var mac = netInterface.GetPhysicalAddress().ToString();
                if (!string.IsNullOrEmpty(mac) && mac != "000000000000" && mac.Length == 12)
                {
                    // Format as XX:XX:XX:XX:XX:XX
                    return string.Join(":", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)));
                }
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Gets the primary network adapter - the one with a default gateway that's connected.
    /// Works with both Ethernet and WiFi connections.
    /// </summary>
    private static NetworkInterface? GetPrimaryNetworkAdapter()
    {
        try
        {
            var candidates = new List<(NetworkInterface Adapter, int Priority)>();

            foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Skip non-operational adapters
                if (netInterface.OperationalStatus != OperationalStatus.Up) continue;
                
                // Skip loopback, tunnel, and virtual adapters
                if (netInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (netInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                if (netInterface.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)) continue;
                if (netInterface.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase)) continue;
                if (netInterface.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)) continue;
                if (netInterface.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase)) continue;
                if (netInterface.Description.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase)) continue;

                var ipProps = netInterface.GetIPProperties();
                
                // Check if this adapter has a default gateway (meaning it can route to the internet)
                var hasGateway = ipProps.GatewayAddresses
                    .Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && 
                             !g.Address.ToString().Equals("0.0.0.0"));

                if (!hasGateway) continue;

                // Check if it has an IPv4 address
                var hasIpv4 = ipProps.UnicastAddresses
                    .Any(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

                if (!hasIpv4) continue;

                // Check if it has a valid MAC address
                var mac = netInterface.GetPhysicalAddress().ToString();
                if (string.IsNullOrEmpty(mac) || mac == "000000000000" || mac.Length != 12) continue;

                // Prioritize by adapter type:
                // 1. Ethernet (most reliable)
                // 2. WiFi (Wireless80211)
                // 3. Other types
                int priority = netInterface.NetworkInterfaceType switch
                {
                    NetworkInterfaceType.Ethernet => 1,
                    NetworkInterfaceType.GigabitEthernet => 1,
                    NetworkInterfaceType.Wireless80211 => 2,
                    _ => 3
                };

                candidates.Add((netInterface, priority));
            }

            // Return the highest priority adapter (lowest priority number)
            return candidates.OrderBy(c => c.Priority).FirstOrDefault().Adapter;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the public IP address of this computer by querying external services.
    /// Results are cached for 1 hour to minimize external API calls.
    /// </summary>
    public async Task<string?> GetPublicIpAddressAsync()
    {
        // Return cached value if still valid
        if (_cachedPublicIp != null && DateTime.UtcNow - _publicIpCacheTime < PublicIpCacheDuration)
        {
            return _cachedPublicIp;
        }

        // List of public IP services to try (in order of preference)
        var ipServices = new[]
        {
            "https://api.ipify.org",
            "https://icanhazip.com",
            "https://checkip.amazonaws.com",
            "https://ipinfo.io/ip"
        };

        foreach (var service in ipServices)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(service);
                var ip = response.Trim();
                
                // Validate it looks like an IP address
                if (System.Net.IPAddress.TryParse(ip, out var parsedIp) && 
                    parsedIp.AddressFamily == AddressFamily.InterNetwork)
                {
                    _cachedPublicIp = ip;
                    _publicIpCacheTime = DateTime.UtcNow;
                    _logger.LogDebug("Public IP retrieved from {Service}: {Ip}", service, ip);
                    return ip;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to get public IP from {Service}", service);
                // Continue to next service
            }
        }

        _logger.LogWarning("Failed to retrieve public IP address from all services");
        return null;
    }
}
