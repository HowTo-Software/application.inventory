// Re-export all middleware for easy importing
// Usage: using ComputerInventory.Api.Middleware;

namespace ComputerInventory.Api.Middleware;

/// <summary>
/// Middleware barrel file - all middleware is accessible from this namespace.
/// 
/// Available middleware:
/// - GlobalExceptionMiddleware: Centralized exception handling with Problem Details
/// - RequestTimingMiddleware: Performance timing headers
/// - RateLimitingMiddleware: API rate limiting
/// - SecurityHeadersMiddleware: Security headers (CSP, X-Frame-Options, etc.)
/// - CorrelationIdMiddleware: Request correlation ID tracking
/// 
/// Request logging is handled by Serilog via UseSerilogRequestLogging().
/// 
/// Usage in Program.cs:
/// 
/// app.UseGlobalExceptionHandler();
/// app.UseSecurityHeaders();
/// app.UseCorrelationId();
/// app.UseRequestTiming();
/// app.UseResponseCompression();
/// app.UseSerilogRequestLogging();
/// app.UseCustomRateLimiting();
/// 
/// </summary>
public static class MiddlewareSummary { }
