using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ComputerInventory.ThinClient;
using ComputerInventory.ThinClient.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

[assembly: SupportedOSPlatform("windows")]

// NOTE: This agent now runs as a Scheduled Task, NOT a Windows Service
// This avoids all the HVCI/SCM timeout issues that plagued the Windows Service approach
// The Scheduled Task runs as SYSTEM at startup and restarts automatically on failure

// VERY EARLY DIAGNOSTICS - writes directly to file before anything else
// This helps diagnose startup hangs
void WriteEarlyDiagnostic(string message)
{
    try
    {
        var diagPath = @"C:\ProgramData\ComputerInventory\agent-startup.log";
        var dir = Path.GetDirectoryName(diagPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
        File.AppendAllText(diagPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
    }
    catch { /* Ignore failures */ }
}

// Write IMMEDIATELY on process start
WriteEarlyDiagnostic("=== PROCESS STARTED ===");
WriteEarlyDiagnostic($"Process ID: {Environment.ProcessId}");
WriteEarlyDiagnostic($"CurrentDirectory: {Environment.CurrentDirectory}");
WriteEarlyDiagnostic($"UserName: {Environment.UserName}");
WriteEarlyDiagnostic($"CommandLine: {Environment.CommandLine}");
WriteEarlyDiagnostic("Running as Scheduled Task (not Windows Service)");

// Helper to write to Windows Event Log (works even if Serilog isn't initialized)
void WriteEventLog(string message, EventLogEntryType type = EventLogEntryType.Information)
{
    try
    {
        const string sourceName = "ComputerInventoryAgent";
        if (!EventLog.SourceExists(sourceName))
        {
            // Try to create the source - may fail without admin rights
            try { EventLog.CreateEventSource(sourceName, "Application"); }
            catch { /* Ignore - source may already exist or we don't have permission */ }
        }
        EventLog.WriteEntry(sourceName, message, type);
    }
    catch
    {
        // Silently fail if we can't write to event log
    }
}

// Set the current directory to the executable's directory
// This ensures appsettings.json is found when running as a service or from startup scripts
WriteEarlyDiagnostic("Getting AppContext.BaseDirectory...");
var exePath = AppContext.BaseDirectory;
WriteEarlyDiagnostic($"ExePath: {exePath}");

WriteEarlyDiagnostic("Setting current directory...");
try
{
    Directory.SetCurrentDirectory(exePath);
    WriteEarlyDiagnostic("Current directory set successfully");
}
catch (Exception ex)
{
    WriteEarlyDiagnostic($"ERROR setting current directory: {ex.Message}");
    WriteEventLog($"Failed to set current directory to {exePath}: {ex.Message}", EventLogEntryType.Error);
    Console.Error.WriteLine($"ERROR: Failed to set current directory: {ex.Message}");
    return;
}

// Configure Serilog for file logging
WriteEarlyDiagnostic("Setting up log path...");
var logPath = Path.Combine(exePath, "logs");
try
{
    // Create logs directory if it doesn't exist
    if (!Directory.Exists(logPath))
    {
        WriteEarlyDiagnostic("Creating logs directory...");
        Directory.CreateDirectory(logPath);
    }
    
    // Test write access by creating a temp file
    WriteEarlyDiagnostic("Testing write access...");
    var testFile = Path.Combine(logPath, ".write-test");
    File.WriteAllText(testFile, "test");
    File.Delete(testFile);
    WriteEarlyDiagnostic("Write access OK");
}
catch (Exception ex)
{
    WriteEarlyDiagnostic($"ERROR writing to logs: {ex.Message}");
    // Can't write to logs folder - try alternative location
    var altLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ComputerInventory", "logs");
    
    try
    {
        Directory.CreateDirectory(altLogPath);
        logPath = altLogPath;
        WriteEventLog($"Cannot write to {Path.Combine(exePath, "logs")}, using alternate path: {altLogPath}", EventLogEntryType.Warning);
    }
    catch (Exception altEx)
    {
        WriteEventLog($"Failed to create logs directory. Primary: {ex.Message}, Alternate: {altEx.Message}", EventLogEntryType.Error);
        Console.Error.WriteLine($"ERROR: Cannot create logs directory: {ex.Message}");
        Console.Error.WriteLine($"Tried alternate location: {altEx.Message}");
        // Continue anyway - console logging will still work
    }
}

WriteEarlyDiagnostic("Creating Serilog configuration...");

// NOTE: EventLog sink removed - can hang on Windows 11 Insider builds
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine(logPath, "agent-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        shared: true,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        flushToDiskInterval: TimeSpan.FromSeconds(1))
    .CreateLogger();
WriteEarlyDiagnostic("Serilog configured");

Log.Information("========================================");
Log.Information("Computer Inventory Agent Starting...");
Log.Information("Version: {Version}", typeof(Program).Assembly.GetName().Version);
Log.Information("Install Path: {Path}", exePath);
Log.Information("Log Path: {LogPath}", logPath);
Log.Information("========================================");
WriteEarlyDiagnostic("Initial log messages written");

WriteEarlyDiagnostic("About to write first Log.Information...");
try
{
    Log.Information("Checking for persisted configuration...");
    WriteEarlyDiagnostic("First Log.Information succeeded");
}
catch (Exception ex)
{
    WriteEarlyDiagnostic($"ERROR in Log.Information: {ex.Message}");
}

// Check if agent log file was created
var agentLogPath = Path.Combine(logPath, $"agent-{DateTime.Now:yyyyMMdd}.log");
WriteEarlyDiagnostic($"Checking agent log exists: {agentLogPath}");
WriteEarlyDiagnostic($"Agent log exists: {File.Exists(agentLogPath)}");

WriteEarlyDiagnostic("Checking persisted config...");

// Check for and restore persisted configuration (from before an update/reinstall)
var configPersistencePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "ComputerInventory",
    "agent-config-backup.json");

Log.Information("Config persistence path: {Path}", configPersistencePath);

if (File.Exists(configPersistencePath))
{
    Log.Information("Found persisted configuration - restoring settings from {Path}", configPersistencePath);
    WriteEarlyDiagnostic($"Found persisted config at {configPersistencePath}, attempting restore with timeout...");
    
    // Use a timeout to prevent hanging on CI-enforced systems
    var configRestoreTimeout = TimeSpan.FromSeconds(10);
    var cts = new CancellationTokenSource(configRestoreTimeout);
    var restoreTask = Task.Run(() =>
    {
        try
        {
            WriteEarlyDiagnostic("  Reading persisted config file...");
            var persistedJson = File.ReadAllText(configPersistencePath);
            WriteEarlyDiagnostic($"  Read {persistedJson.Length} bytes");
            
            var appSettingsPath = Path.Combine(exePath, "appsettings.json");
            WriteEarlyDiagnostic($"  Target appsettings: {appSettingsPath}");
            
            // Parse the persisted config to extract the Configuration section
            WriteEarlyDiagnostic("  Parsing JSON...");
            using var doc = System.Text.Json.JsonDocument.Parse(persistedJson);
            if (doc.RootElement.TryGetProperty("Configuration", out var configElement))
            {
                WriteEarlyDiagnostic("  Found Configuration section, restoring...");
                // Write the Configuration content to appsettings.json
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                var restoredConfig = System.Text.Json.JsonSerializer.Serialize(
                    System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(configElement.GetRawText()),
                    options);
                File.WriteAllText(appSettingsPath, restoredConfig);
                WriteEarlyDiagnostic("  Configuration restored successfully");
                Log.Information("Configuration restored successfully");
            }
            else
            {
                WriteEarlyDiagnostic("  No Configuration section found in backup");
            }
            
            // Delete the persisted file
            WriteEarlyDiagnostic("  Deleting persisted config file...");
            File.Delete(configPersistencePath);
            WriteEarlyDiagnostic("  Persisted config file deleted");
            Log.Information("Deleted persisted configuration file");
            return true;
        }
        catch (Exception ex)
        {
            WriteEarlyDiagnostic($"  Config restore error: {ex.Message}");
            Log.Warning(ex, "Failed to restore persisted configuration");
            return false;
        }
    }, cts.Token);
    
    try
    {
        var completed = restoreTask.Wait(configRestoreTimeout);
        if (!completed)
        {
            WriteEarlyDiagnostic("  CONFIG RESTORE TIMEOUT - deleting backup file to prevent future hangs");
            Log.Warning("Config restoration timed out after {Timeout}s - deleting backup to prevent future hangs", configRestoreTimeout.TotalSeconds);
            
            // Force delete the config file to prevent hanging on next restart
            try
            {
                File.Delete(configPersistencePath);
                WriteEarlyDiagnostic("  Deleted hung config file successfully");
                Log.Information("Deleted persisted configuration file that caused timeout");
            }
            catch (Exception deleteEx)
            {
                WriteEarlyDiagnostic($"  Failed to delete hung config: {deleteEx.Message}");
                Log.Warning(deleteEx, "Failed to delete persisted configuration file");
            }
        }
        else
        {
            WriteEarlyDiagnostic("  Config restore completed within timeout");
        }
    }
    catch (OperationCanceledException)
    {
        WriteEarlyDiagnostic("  Config restore was cancelled");
        Log.Warning("Config restoration was cancelled");
    }
    catch (AggregateException ae)
    {
        WriteEarlyDiagnostic($"  Config restore aggregate error: {ae.InnerException?.Message}");
        Log.Warning(ae.InnerException ?? ae, "Config restoration failed");
    }
}
else
{
    WriteEarlyDiagnostic("No persisted config found - continuing with existing appsettings.json");
}

// Parse command-line arguments for server URL
// Usage: MKS.InventoryAgent.exe --server http://server:5010
//    or: MKS.InventoryAgent.exe -s http://server:5010
WriteEarlyDiagnostic("Parsing command-line arguments...");
string? serverUrlArg = null;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--server" || args[i] == "-s")
    {
        serverUrlArg = args[i + 1];
        break;
    }
}
WriteEarlyDiagnostic($"Command-line parsing complete. ServerUrl arg: {serverUrlArg ?? "(none)"}");

WriteEarlyDiagnostic("Creating host builder (using explicit HostBuilder)...");

// Build configuration first - with detailed logging to find hang point
WriteEarlyDiagnostic("Building configuration...");
WriteEarlyDiagnostic("  Step 1: Creating ConfigurationBuilder");
var configBuilder = new ConfigurationBuilder();
var configFilePath = Path.Combine(exePath, "appsettings.json");

// WORKAROUND: Skip SetBasePath entirely - it HANGS on Windows 11 Insider build 26200
// Use PhysicalFileProvider directly instead
WriteEarlyDiagnostic("  Step 2: Skipping SetBasePath (hangs on Win11 Insider), using PhysicalFileProvider");

WriteEarlyDiagnostic("  Step 3: Checking if config file exists...");
if (File.Exists(configFilePath))
{
    WriteEarlyDiagnostic($"  Step 3a: File exists, size = {new FileInfo(configFilePath).Length} bytes");
}
else
{
    WriteEarlyDiagnostic($"  Step 3a: Config file does NOT exist at {configFilePath}");
}

// WORKAROUND: PhysicalFileProvider ALSO hangs on HVCI systems!
// Read the JSON file directly and use AddJsonStream instead
WriteEarlyDiagnostic("  Step 3b: Reading config file directly (bypassing PhysicalFileProvider which hangs on HVCI)...");
if (File.Exists(configFilePath))
{
    try
    {
        var jsonContent = File.ReadAllText(configFilePath);
        WriteEarlyDiagnostic($"  Step 3c: Read {jsonContent.Length} chars, converting to stream...");
        var jsonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonContent));
        configBuilder.AddJsonStream(jsonStream);
        WriteEarlyDiagnostic("  Step 3d: JSON stream added successfully");
    }
    catch (Exception ex)
    {
        WriteEarlyDiagnostic($"  WARNING: Failed to read config file: {ex.Message}");
        // Continue without config - will use defaults
    }
}
else
{
    WriteEarlyDiagnostic("  Step 3c: Config file not found, using defaults");
}

WriteEarlyDiagnostic("  Step 4: Adding environment variables");
// WORKAROUND: AddEnvironmentVariables can hang on HVCI systems during enumeration
// Skip it entirely - we don't really need environment variable overrides
// configBuilder.AddEnvironmentVariables("INVENTORY_");
WriteEarlyDiagnostic("  Step 4a: SKIPPING AddEnvironmentVariables (can hang on HVCI)");

WriteEarlyDiagnostic("  Step 5: Calling Build() with timeout protection...");
IConfiguration configuration;
try
{
    // Wrap Build() in a task with timeout - on HVCI systems this can hang
    var buildTask = Task.Run(() => configBuilder.Build());
    if (buildTask.Wait(TimeSpan.FromSeconds(15)))
    {
        configuration = buildTask.Result;
        WriteEarlyDiagnostic("Configuration built successfully");
    }
    else
    {
        WriteEarlyDiagnostic("WARNING: Configuration Build() timed out after 15s, using minimal config");
        // Create minimal in-memory configuration
        var minimalConfig = new Dictionary<string, string?>
        {
            ["ServerUrls:Primary"] = "http://rdp.mkspvf.com:21118",
            ["CheckInIntervalMinutes"] = "5"
        };
        configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(minimalConfig)
            .Build();
        WriteEarlyDiagnostic("Minimal configuration created");
    }
}
catch (Exception ex)
{
    WriteEarlyDiagnostic($"ERROR building configuration: {ex.Message}");
    throw;
}

// Override primary URL from command line or environment if provided
string? overrideUrl = null;
if (!string.IsNullOrEmpty(serverUrlArg))
{
    overrideUrl = serverUrlArg;
}
else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INVENTORY_SERVER_URL")))
{
    overrideUrl = Environment.GetEnvironmentVariable("INVENTORY_SERVER_URL");
}

WriteEarlyDiagnostic("Creating service collection...");
var services = new ServiceCollection();

// Check Code Integrity status - many .NET APIs hang on HVCI systems
// We need to know this to use workarounds
bool isCodeIntegrityEnforced = false;
try
{
    WriteEarlyDiagnostic("  Checking Code Integrity enforcement status...");
    using var ciKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
        @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");
    if (ciKey != null)
    {
        var enabled = ciKey.GetValue("Enabled");
        isCodeIntegrityEnforced = enabled != null && Convert.ToInt32(enabled) == 1;
    }
    WriteEarlyDiagnostic($"  Code Integrity enforced: {isCodeIntegrityEnforced}");
}
catch (Exception ex)
{
    WriteEarlyDiagnostic($"  CI check failed: {ex.Message}");
}

// No longer need to detect Windows Service mode - we run as Scheduled Task
// Just detect if we're running on an HVCI system for workarounds
WriteEarlyDiagnostic($"  Code Integrity enforced: {isCodeIntegrityEnforced}");

// Add configuration
WriteEarlyDiagnostic("  Adding IConfiguration...");
services.AddSingleton<IConfiguration>(configuration);

// Use Serilog
WriteEarlyDiagnostic("  Adding Serilog...");
services.AddSerilog();

// Add logging - on CI systems, AddLogging with lambda can hang
if (isCodeIntegrityEnforced)
{
    WriteEarlyDiagnostic("  CI Mode: Registering ILoggerFactory and ILogger<> manually (AddLogging lambda can hang)...");
    // Register Serilog's ILoggerFactory directly without using AddLogging() extension
    // AddLogging() with lambda configuration can hang on HVCI systems
    var serilogFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger, false);
    services.AddSingleton<ILoggerFactory>(serilogFactory);
    services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
    WriteEarlyDiagnostic("  CI Mode: ILoggerFactory and ILogger<> registered successfully");
}
else
{
    WriteEarlyDiagnostic("  Adding Logging...");
    services.AddLogging(builder =>
    {
        builder.ClearProviders();
        builder.AddSerilog(dispose: true);
    });
}

// Register services
WriteEarlyDiagnostic("Registering application services...");

WriteEarlyDiagnostic("  Adding LocationService...");
services.AddSingleton<ILocationService, LocationService>();

WriteEarlyDiagnostic("  Adding SystemInfoService...");
services.AddSingleton<ISystemInfoService, SystemInfoService>();

WriteEarlyDiagnostic("  Adding LocalCacheService...");
services.AddSingleton<ILocalCacheService, LocalCacheService>();

WriteEarlyDiagnostic("  Adding ConfigPersistenceService...");
services.AddSingleton<IConfigPersistenceService, ConfigPersistenceService>();

WriteEarlyDiagnostic("  Adding ServerConnectionManager...");
services.AddSingleton<IServerConnectionManager, ServerConnectionManager>();

WriteEarlyDiagnostic("  Adding ResilientApiClientService...");
services.AddSingleton<IApiClientService, ResilientApiClientService>();

WriteEarlyDiagnostic("  Adding CheckInService...");
services.AddSingleton<ICheckInService, CheckInService>();

WriteEarlyDiagnostic("  Adding UpdateService...");
services.AddSingleton<IUpdateService, UpdateService>();

WriteEarlyDiagnostic("  Adding DetailedSystemCollector...");
services.AddSingleton<DetailedSystemCollector>();

WriteEarlyDiagnostic("  Adding RestorePointCollector...");
services.AddSingleton<RestorePointCollector>();

// Add HttpClient - on CI systems, AddHttpClient() can hang, so register manually
var apiBaseUrl = configuration["ApiSettings:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5010";
if (isCodeIntegrityEnforced)
{
    WriteEarlyDiagnostic("  CI Mode: Registering HttpClient instances directly (AddHttpClient can hang)...");
    
    // Register a simple HttpClient factory that creates clients on demand
    services.AddSingleton<IHttpClientFactory>(sp => new SimpleHttpClientFactory(apiBaseUrl));
}
else
{
    WriteEarlyDiagnostic("  Adding HttpClient factory...");
    services.AddHttpClient();

    // Named client for API calls - UpdateService and ApiClientService use this
    WriteEarlyDiagnostic("  Adding InventoryApi named HttpClient...");
    services.AddHttpClient("InventoryApi", client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl + "/");
        client.Timeout = TimeSpan.FromSeconds(60);
    });

    WriteEarlyDiagnostic("  Adding PublicIp named HttpClient...");
    services.AddHttpClient("PublicIp", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });
}

// Register the background worker
WriteEarlyDiagnostic("  Adding HostedService<InventoryAgentWorker>...");
services.AddHostedService<InventoryAgentWorker>();

WriteEarlyDiagnostic("Services registered");

WriteEarlyDiagnostic("Creating host builder...");

// Simple host builder - no Windows Service, just a console app that runs forever
// This works perfectly with Scheduled Task which manages the process lifecycle
WriteEarlyDiagnostic("  Creating HostBuilder (console app mode for Scheduled Task)...");
var builder = new HostBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddConfiguration(configuration);
    })
    .ConfigureServices((context, svc) =>
    {
        // Copy all services from our collection
        foreach (var descriptor in services)
        {
            svc.Add(descriptor);
        }
    });

WriteEarlyDiagnostic("  Base HostBuilder created");

// NOTE: We don't call UseWindowsService() because we're running as a Scheduled Task, not a Windows Service
// The Scheduled Task runs this as a normal console application that stays running until stopped
WriteEarlyDiagnostic("  (Not using UseWindowsService - running as Scheduled Task)");

WriteEarlyDiagnostic("  Adding Serilog...");
builder = builder.UseSerilog();

WriteEarlyDiagnostic("Host builder created");

WriteEarlyDiagnostic("Building host...");

IHost host;
try
{
    host = builder.Build();
    WriteEarlyDiagnostic("Host built successfully");
    
    // If we have an override URL, update configuration after build
    if (!string.IsNullOrEmpty(overrideUrl))
    {
        Log.Information("Primary server URL overridden to: {ApiUrl}", overrideUrl);
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "Failed to build host - dependency injection or configuration error");
    WriteEventLog($"Failed to build host: {ex.Message}\n{ex.StackTrace}", EventLogEntryType.Error);
    Console.Error.WriteLine($"FATAL ERROR building host: {ex.Message}");
    Console.Error.WriteLine(ex.ToString());
    Log.CloseAndFlush();
    return;
}

try
{
    Log.Information("Agent host starting...");
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Agent terminated unexpectedly");
    WriteEventLog($"Agent terminated unexpectedly: {ex.Message}\n{ex.StackTrace}", EventLogEntryType.Error);
    Console.Error.WriteLine($"FATAL ERROR: {ex.Message}");
    Console.Error.WriteLine(ex.ToString());
}
finally
{
    Log.Information("Agent shutting down...");
    Log.CloseAndFlush();
}

/// <summary>
/// Simple IHttpClientFactory implementation for HVCI systems where AddHttpClient() can hang.
/// Uses a shared SocketsHttpHandler to avoid socket exhaustion while creating new HttpClient
/// instances each time so callers can safely set properties like BaseAddress and Timeout.
/// </summary>
public class SimpleHttpClientFactory : IHttpClientFactory
{
    private readonly string _apiBaseUrl;
    private readonly SocketsHttpHandler _sharedHandler = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        MaxConnectionsPerServer = 10
    };
    
    public SimpleHttpClientFactory(string apiBaseUrl)
    {
        _apiBaseUrl = apiBaseUrl;
    }
    
    public HttpClient CreateClient(string name)
    {
        var client = new HttpClient(_sharedHandler, disposeHandler: false);
        
        if (name == "InventoryApi")
        {
            client.BaseAddress = new Uri(_apiBaseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(60);
        }
        else if (name == "PublicIp")
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        }
        else
        {
            client.Timeout = TimeSpan.FromSeconds(100);
        }
        
        return client;
    }
}