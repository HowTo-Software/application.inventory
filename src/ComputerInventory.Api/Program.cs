using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ComputerInventory.Api.Authorization;
using ComputerInventory.Api.Hubs;
using ComputerInventory.Api.Middleware;
using ComputerInventory.Api.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Infrastructure.Data;
using ComputerInventory.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog as the logging provider for structured log output.
// Reads sink/enricher/level configuration from the "Serilog" section in appsettings.json.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ============================================
// SERVICES CONFIGURATION
// ============================================

// Cap request body size to prevent memory exhaustion from oversized payloads.
// Endpoints that need larger uploads (e.g., package upload) use [RequestSizeLimit] to override.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 30 * 1024 * 1024; // 30 MB global default
});

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 30 * 1024 * 1024; // 30 MB (IIS in-process hosting)
});

// Add response compression for better performance
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
    options.Providers.Add<BrotliCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] 
    { 
        "application/json",
        "application/problem+json",
        "text/plain"
    });
});

// Configure Brotli compression (better than gzip)
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

// Add controllers with optimized JSON settings
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Register FluentValidation validators and automatic validation filter
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ============================================
// STARTUP CONFIGURATION VALIDATION
// ============================================
// Validate all required configuration keys at startup to fail fast
// instead of silently using defaults or crashing at runtime.

var configErrors = new List<string>();
var configWarnings = new List<string>();

// Safeguard: block Development environment when running inside a container.
// Development mode exposes stack traces, inner exceptions, and sensitive EF Core data.
if (builder.Environment.IsDevelopment() &&
    string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase))
{
    configErrors.Add(
        "ASPNETCORE_ENVIRONMENT is set to 'Development' inside a container. " +
        "This exposes stack traces and sensitive data. " +
        "Set ASPNETCORE_ENVIRONMENT=Production (or remove the override) before deploying.");
}

// --- ConnectionStrings ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    configErrors.Add("ConnectionStrings:DefaultConnection is not configured. " +
        "Set the 'ConnectionStrings__DefaultConnection' environment variable or use a .env file.");
}

// --- JwtSettings ---
var jwtSecretKey = builder.Configuration["JwtSettings:SecretKey"];
if (string.IsNullOrWhiteSpace(jwtSecretKey))
{
    configErrors.Add("JwtSettings:SecretKey is not configured. " +
        "Set the 'JwtSettings__SecretKey' environment variable or use a .env file.");
}
else if (jwtSecretKey.Length < 32)
{
    configErrors.Add("JwtSettings:SecretKey must be at least 32 characters for HMAC-SHA256.");
}

if (string.IsNullOrWhiteSpace(builder.Configuration["JwtSettings:Issuer"]))
{
    configErrors.Add("JwtSettings:Issuer is not configured.");
}

if (string.IsNullOrWhiteSpace(builder.Configuration["JwtSettings:Audience"]))
{
    configErrors.Add("JwtSettings:Audience is not configured.");
}

ValidatePositiveInt("JwtSettings:ExpirationInMinutes");
ValidatePositiveInt("JwtSettings:RefreshTokenExpirationInDays");

// --- AgentSettings ---
var agentApiKey = builder.Configuration["AgentSettings:ApiKey"];
if (string.IsNullOrWhiteSpace(agentApiKey))
{
    if (builder.Environment.IsProduction())
    {
        configErrors.Add("AgentSettings:ApiKey is not configured. Agent authentication will reject all requests. " +
            "Set the 'AgentSettings__ApiKey' environment variable or use a .env file.");
    }
    else
    {
        configWarnings.Add("AgentSettings:ApiKey is not configured. " +
            "Agent endpoints will reject requests until a key is set.");
    }
}

if (string.IsNullOrWhiteSpace(builder.Configuration["AgentSettings:ServerUrl"]))
{
    if (builder.Environment.IsProduction())
    {
        configErrors.Add("AgentSettings:ServerUrl is not configured. Install script generation will fail.");
    }
    else
    {
        configWarnings.Add("AgentSettings:ServerUrl is not configured. " +
            "Install script generation will use an empty URL.");
    }
}

// --- GeoLocation ---
var geoBaseUrl = builder.Configuration["GeoLocation:BaseUrl"];
if (string.IsNullOrWhiteSpace(geoBaseUrl))
{
    configWarnings.Add("GeoLocation:BaseUrl is not configured — IP-based geo-location lookups will use the default provider.");
}
else if (!Uri.TryCreate(geoBaseUrl, UriKind.Absolute, out var geoUri) ||
         (geoUri.Scheme != "https" && geoUri.Scheme != "http"))
{
    configErrors.Add($"GeoLocation:BaseUrl must be a valid HTTP(S) URL (got '{geoBaseUrl}').");
}

var geoTemplate = builder.Configuration["GeoLocation:LookupPathTemplate"];
if (!string.IsNullOrWhiteSpace(geoTemplate) && !geoTemplate.Contains("{ip}"))
{
    configErrors.Add("GeoLocation:LookupPathTemplate must contain the '{ip}' placeholder.");
}

// --- MapSettings ---
ValidatePositiveInt("MapSettings:GeoLocationRefreshIntervalHours");

// --- DatabaseMaintenance ---
ValidatePositiveInt("DatabaseMaintenance:OfflineCheckIntervalMinutes");
ValidatePositiveInt("DatabaseMaintenance:CleanupIntervalHours");
ValidatePositiveInt("DatabaseMaintenance:OfflineThresholdMinutes");
ValidatePositiveInt("DatabaseMaintenance:SystemDetailsRetentionDays");
ValidatePositiveInt("DatabaseMaintenance:RestorePointRetentionDays");
ValidatePositiveInt("DatabaseMaintenance:HistoryRetentionDays");
ValidatePositiveInt("DatabaseMaintenance:MaxSystemDetailsPerComputer");
ValidatePositiveInt("DatabaseMaintenance:MaxRestorePointsPerComputer");

// --- Emit warnings ---
foreach (var warning in configWarnings)
{
    Console.WriteLine($"WARNING: {warning}");
}

// --- Fail fast on errors ---
if (configErrors.Count > 0)
{
    var message = "Startup configuration validation failed:\n  - " +
        string.Join("\n  - ", configErrors);
    throw new InvalidOperationException(message);
}

void ValidatePositiveInt(string key)
{
    var value = builder.Configuration[key];
    if (value is null)
    {
        configWarnings.Add($"{key} is not configured — falling back to hardcoded default.");
    }
    else if (!int.TryParse(value, out var parsed) || parsed <= 0)
    {
        configErrors.Add($"{key} must be a positive integer (got '{value}').");
    }
}

// Configure Entity Framework with SQL Server and connection resiliency
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(60);
            sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            sqlOptions.MinBatchSize(5);
            sqlOptions.MaxBatchSize(100);
        });
    
    // Use snake_case naming convention for all tables and columns
    options.UseSnakeCaseNamingConvention();
    
    // Enable detailed errors in development (but NOT sensitive data logging, which can leak passwords/tokens)
    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
    }
});

// Register audit infrastructure
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Register repositories
builder.Services.AddScoped<IComputerRepository, ComputerRepository>();
builder.Services.AddScoped<IComputerHistoryRepository, ComputerHistoryRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();
builder.Services.AddScoped<IGeoLocationRepository, GeoLocationRepository>();
builder.Services.AddScoped<ILookupRepository, LookupRepository>();
builder.Services.AddScoped<IVersionHistoryRepository, VersionHistoryRepository>();
builder.Services.AddScoped<IRestorePointRepository, RestorePointRepository>();
builder.Services.AddScoped<ISystemDetailsRepository, SystemDetailsRepository>();
builder.Services.AddScoped<IPackageRepository, PackageRepository>();
builder.Services.AddScoped<IComputerOwnershipRepository, ComputerOwnershipRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IInstallTokenRepository, InstallTokenRepository>();
builder.Services.AddScoped<IInviteCodeRepository, InviteCodeRepository>();
builder.Services.AddScoped<IAppSettingRepository, AppSettingRepository>();
builder.Services.AddScoped<IDatabaseMaintenanceRepository, DatabaseMaintenanceRepository>();
builder.Services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();
builder.Services.AddScoped<IHealthCheckRepository, HealthCheckRepository>();

// Register services
builder.Services.AddSingleton<IPasswordValidator, PasswordValidator>();
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ReferenceDataResolver>();
builder.Services.AddSingleton<IErrorAnalyzerService, ErrorAnalyzerService>();
builder.Services.Configure<GeoLocationSettings>(builder.Configuration.GetSection(GeoLocationSettings.SectionName));
builder.Services.AddHttpClient<IGeoLocationService, GeoLocationService>()
    .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(3,
        attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (outcome, delay, attempt, _) =>
        {
            Log.Warning("GeoLocation HTTP retry {Attempt} after {Delay}s: {Reason}",
                attempt, delay.TotalSeconds,
                outcome.Exception?.Message ?? $"HTTP {(int?)outcome.Result?.StatusCode}");
        }));

// Register caching service — use Redis-backed distributed cache when configured, otherwise in-memory
var cacheOptions = builder.Configuration.GetSection("CacheSettings").Get<CacheServiceOptions>() ?? new CacheServiceOptions();
builder.Services.AddSingleton(cacheOptions);

var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "InvCache:";
    });
    builder.Services.AddSingleton<ICacheService, RedisCacheService>();
    Log.Information("Using Redis distributed cache at {RedisEndpoint}", redisConnection);
}
else
{
    builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();
    Log.Information("Using in-memory cache (no Redis connection configured)");
}

// Register login brute-force protection (per-username progressive lockout)
builder.Services.AddSingleton<ILoginRateLimiter, LoginRateLimiter>();

// Register rate limit store — use Redis-backed distributed store when configured, otherwise in-memory
if (!string.IsNullOrEmpty(redisConnection))
{
    // Redis cache already registered above — add rate limit store
    builder.Services.AddSingleton<IRateLimitStore, DistributedRateLimitStore>();
}
else
{
    builder.Services.AddSingleton<IRateLimitStore, InMemoryRateLimitStore>();
}

// Register agent file path settings
var agentFileSettings = builder.Configuration.GetSection(AgentFileSettings.SectionName).Get<AgentFileSettings>() ?? new AgentFileSettings();
builder.Services.AddSingleton(agentFileSettings);

// Register update info cache (avoids disk I/O on every heartbeat)
builder.Services.AddSingleton<IUpdateInfoCacheService, UpdateInfoCacheService>();

// Register version info cache (avoids disk I/O on every version API call)
builder.Services.AddSingleton<IVersionInfoCacheService, VersionInfoCacheService>();

// Register centralized package versioning service
builder.Services.AddScoped<IPackageService, PackageService>();

// Register install token service for secure install script access
builder.Services.AddScoped<IInstallTokenService, InstallTokenService>();

// Register invite code service for gating user registration
builder.Services.AddScoped<IInviteCodeService, InviteCodeService>();

// Register background task queue for reliable background operations
builder.Services.AddBackgroundTaskQueue(builder.Configuration);

// Register database maintenance service
builder.Services.AddHostedService<DatabaseMaintenanceService>();

// Register SignalR for real-time update progress
builder.Services.AddSignalR();

// Add health checks
builder.Services.AddCustomHealthChecks();

// Persist DataProtection keys so they survive container restarts
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/keys"));

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey!)),
        ClockSkew = TimeSpan.FromMinutes(1) // Reduce default clock skew for better security
    };

    // Add token validation events for logging
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // 1. Allow SignalR to receive JWT from query string (fallback)
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
                return Task.CompletedTask;
            }

            // 2. Extract JWT from httpOnly cookie when no Authorization header is present
            if (!context.Request.Headers.ContainsKey("Authorization")
                && context.Request.Cookies.TryGetValue("access_token", out var cookieToken)
                && !string.IsNullOrEmpty(cookieToken))
            {
                context.Token = cookieToken;
            }

            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Authentication failed: {Error}", context.Exception.Message);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("TechnicianOrAdmin", policy => policy.RequireRole("Admin", "Technician"));
    options.AddPolicy("AllUsers", policy => policy.RequireRole("Admin", "Technician", "Viewer"));
});

// Per-computer authorization: department + ownership-scoped access checks
builder.Services.AddScoped<IAuthorizationHandler, ComputerAuthorizationHandler>();

// Configure CORS
var corsOrigins = builder.Configuration.GetSection("CorsOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        if (corsOrigins.Length == 1 && corsOrigins[0] == "*")
        {
            policy.AllowAnyOrigin();
        }
        else if (corsOrigins.Length > 0)
        {
            policy.WithOrigins(corsOrigins)
                  .AllowCredentials();
        }

        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders(
                  "X-Correlation-Id",
                  "X-Response-Time",
                  "X-Request-Id",
                  "X-RateLimit-Limit",
                  "X-RateLimit-Remaining",
                  "X-RateLimit-Reset");
    });
});

// Configure Swagger with better documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Computer Inventory API",
        Version = "v1",
        Description = "API for managing computer inventory with agent support",
        Contact = new OpenApiContact
        {
            Name = "IT Support",
            Email = "support@example.com"
        }
    });
    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Enable XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Add ProblemDetails support
builder.Services.AddProblemDetails();

var app = builder.Build();

// ============================================
// MIDDLEWARE PIPELINE (ORDER MATTERS!)
// ============================================

// 1. Exception handling (first to catch all errors)
app.UseGlobalExceptionHandler();

// 2. Security headers
app.UseSecurityHeaders();

// 2b. HTTPS redirection
app.UseHttpsRedirection();

// 3. Correlation ID for request tracking
app.UseCorrelationId();

// 4. Request timing and performance headers
app.UseRequestTiming(options =>
{
    options.SlowRequestThresholdMs = 2000;
});

// 5. Response compression
app.UseResponseCompression();

// 6. Structured request logging via Serilog (replaces custom StringBuilder-based middleware)
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);

        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        if (!string.IsNullOrEmpty(userAgent))
            diagnosticContext.Set("UserAgent", userAgent[..Math.Min(100, userAgent.Length)]);

        if (httpContext.User.Identity?.IsAuthenticated == true)
            diagnosticContext.Set("AuthenticatedUser", httpContext.User.Identity.Name);
    };
});

// 7. Swagger (development only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Computer Inventory API v1");
        c.DisplayRequestDuration();
    });
}

// 8. CORS (before auth)
app.UseCors("AllowBlazorClient");

// 9. Rate limiting (after CORS, before auth)
app.UseCustomRateLimiting(app.Configuration, options =>
{
    options.AgentApiKey = app.Configuration["AgentSettings:ApiKey"];
});

// 10. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 11. Map controllers
app.MapControllers();

// 12. Map SignalR hubs
app.MapHub<UpdateProgressHub>("/hubs/update-progress");

// 12. Health check endpoints (no auth required)
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // Just check if app is running
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// ============================================
// CONVENIENCE ROUTES
// ============================================

// Add convenient shortcut routes for agent installation
app.MapGet("/agent/install.ps1", (HttpContext context) =>
{
    var token = context.Request.Query["token"].FirstOrDefault();
    var url = string.IsNullOrEmpty(token)
        ? "/api/agent/download/install-script"
        : $"/api/agent/download/install-script?token={Uri.EscapeDataString(token)}";
    return Results.Redirect(url, permanent: false);
});

app.MapGet("/agent/install", (HttpContext context) =>
{
    var token = context.Request.Query["token"].FirstOrDefault();
    var url = string.IsNullOrEmpty(token)
        ? "/api/agent/download/install-script"
        : $"/api/agent/download/install-script?token={Uri.EscapeDataString(token)}";
    return Results.Redirect(url, permanent: false);
});

// ============================================
// STARTUP LOGGING
// ============================================

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("===========================================");
logger.LogInformation("Computer Inventory API Starting");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
logger.LogInformation("===========================================");

// Apply pending SQL migrations from the configured directory (default: "migrations" next to the app).
// The runner creates a __migration_history tracking table and applies scripts in filename order.
var migrationsPath = app.Configuration["MigrationSettings:MigrationsPath"]
    ?? Path.Combine(app.Environment.ContentRootPath, "migrations");
if (Directory.Exists(migrationsPath))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var migrationRunner = new DatabaseMigrationRunner(
        scope.ServiceProvider.GetRequiredService<ILogger<DatabaseMigrationRunner>>());
    await migrationRunner.ApplyMigrationsAsync(dbContext, migrationsPath);
}
else
{
    logger.LogInformation("Migrations directory not found at {Path} — skipping automatic migrations", migrationsPath);
}

app.Run();
