using Blazored.LocalStorage;
using ComputerInventory.Web;
using ComputerInventory.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Add logging configuration
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add Blazored LocalStorage first (needed by AuthorizationMessageHandler)
builder.Services.AddBlazoredLocalStorage();

// Register AuthTokenProvider as singleton (shared session state tracking)
builder.Services.AddSingleton<AuthTokenProvider>();

// Configure HttpClient with authorization handler
builder.Services.AddScoped<AuthorizationMessageHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthorizationMessageHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler)
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
        Timeout = TimeSpan.FromSeconds(30)
    };
});

// Add the new ApiClient for improved error handling
builder.Services.AddScoped<IApiClient, ApiClient>();

// Add Authentication
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Register services
builder.Services.AddScoped<IComputerService, ComputerService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<IReportsService, ReportsService>();
builder.Services.AddScoped<IVersionService, VersionService>();
builder.Services.AddScoped<IMapService, MapService>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IPackageService, PackageService>();
builder.Services.AddScoped<IInviteCodeService, InviteCodeService>();

// Real-time update progress via SignalR
builder.Services.AddScoped<UpdateProgressHubService>();

await builder.Build().RunAsync();
