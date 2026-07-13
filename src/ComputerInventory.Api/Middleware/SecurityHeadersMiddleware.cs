using System.Net;

namespace ComputerInventory.Api.Middleware;

/// <summary>
/// Middleware that adds CORS headers and security headers to all responses.
/// Provides defense-in-depth for the API.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeadersOptions _options;

    public SecurityHeadersMiddleware(RequestDelegate next, SecurityHeadersOptions? options = null)
    {
        _next = next;
        _options = options ?? new SecurityHeadersOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip strict security headers for Swagger UI (it needs inline scripts/styles)
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        var isSwaggerPath = path.StartsWith("/swagger") || path.Contains("swagger");
        
        // Add security headers before processing request
        AddSecurityHeaders(context, isSwaggerPath);

        await _next(context);
    }

    private void AddSecurityHeaders(HttpContext context, bool isSwaggerPath)
    {
        var headers = context.Response.Headers;

        // Prevent MIME type sniffing
        if (_options.AddXContentTypeOptions)
        {
            headers["X-Content-Type-Options"] = "nosniff";
        }

        // Prevent clickjacking (allow for Swagger)
        if (_options.AddXFrameOptions && !isSwaggerPath)
        {
            headers["X-Frame-Options"] = "DENY";
        }

        // Referrer Policy
        if (_options.AddReferrerPolicy)
        {
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        }

        // Content Security Policy
        if (_options.AddContentSecurityPolicy)
        {
            if (isSwaggerPath)
            {
                // Swagger UI needs inline scripts and styles
                headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none';";
            }
            else
            {
                // Blazor WASM requires 'unsafe-eval' (WebAssembly) and 'unsafe-inline' (inline scripts in index.html)
                // CDN sources: Bootstrap (css/js/fonts), Bootstrap Icons (css/fonts), Leaflet (css/js)
                // Map tiles: OpenStreetMap
                // SignalR: WebSocket connections
                // blob: needed for client-side file downloads (URL.createObjectURL)
                headers["Content-Security-Policy"] = string.Join("; ",
                    "default-src 'self'",
                    "script-src 'self' 'unsafe-eval' 'unsafe-inline' https://cdn.jsdelivr.net https://unpkg.com",
                    "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://unpkg.com",
                    "img-src 'self' data: blob: https://tile.openstreetmap.org",
                    "font-src 'self' https://cdn.jsdelivr.net",
                    "connect-src 'self' ws: wss:",
                    "worker-src 'self' blob:",
                    "frame-ancestors 'none'",
                    "base-uri 'self'",
                    "form-action 'self'",
                    "object-src 'none'"
                );
            }
        }

        // Permissions Policy
        if (_options.AddPermissionsPolicy)
        {
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        }

        // Cache control — only apply no-store to API endpoints, not health checks, swagger, or static content
        if (_options.AddCacheControl && !headers.ContainsKey("Cache-Control"))
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            var isApiPath = path.StartsWith("/api/");

            if (isApiPath)
            {
                headers["Cache-Control"] = "no-store, no-cache, must-revalidate, proxy-revalidate";
                headers["Pragma"] = "no-cache";
                headers["Expires"] = "0";
            }
        }

        // Prevent Adobe Flash/Acrobat from loading cross-domain data
        if (_options.AddXPermittedCrossDomainPolicies)
        {
            headers["X-Permitted-Cross-Domain-Policies"] = "none";
        }

        // HTTP Strict Transport Security (HSTS)
        if (_options.AddStrictTransportSecurity)
        {
            headers["Strict-Transport-Security"] = $"max-age={_options.HstsMaxAgeSeconds}; includeSubDomains";
        }

        // Remove server identification headers
        if (_options.RemoveServerHeader)
        {
            headers.Remove("Server");
            headers.Remove("X-Powered-By");
            headers.Remove("X-AspNet-Version");
        }
    }
}

/// <summary>
/// Configuration options for security headers middleware.
/// </summary>
public class SecurityHeadersOptions
{
    public bool AddXContentTypeOptions { get; set; } = true;
    public bool AddXFrameOptions { get; set; } = true;
    public bool AddReferrerPolicy { get; set; } = true;
    public bool AddContentSecurityPolicy { get; set; } = true;
    public bool AddPermissionsPolicy { get; set; } = true;
    public bool AddCacheControl { get; set; } = true;
    public bool AddXPermittedCrossDomainPolicies { get; set; } = true;
    public bool AddStrictTransportSecurity { get; set; } = true;
    public int HstsMaxAgeSeconds { get; set; } = 31536000; // 1 year
    public bool RemoveServerHeader { get; set; } = true;
}

/// <summary>
/// Extension methods for registering the security headers middleware.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app,
        Action<SecurityHeadersOptions>? configure = null)
    {
        var options = new SecurityHeadersOptions();
        configure?.Invoke(options);

        return app.UseMiddleware<SecurityHeadersMiddleware>(options);
    }
}
