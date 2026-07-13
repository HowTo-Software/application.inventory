using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ComputerInventory.Api.Middleware;

/// <summary>
/// Global exception handling middleware that provides consistent error responses
/// following RFC 7807 Problem Details specification.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    private const int MaxBodyCaptureBytes = 8192;

    private static readonly Regex SensitiveFieldPattern = new(
        @"(""(?:password|secret|token|apikey|api_key|authorization|credit_card|ssn)""\s*:\s*)""[^""]*""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — not an error. Suppress the response.
            _logger.LogDebug("Request cancelled by client. Path: {Path}", context.Request.Path);
            context.Response.StatusCode = 499; // Client Closed Request (nginx convention)
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.TraceIdentifier;
        
        var requestContext = await CaptureRequestContextAsync(context);

        // Log the exception with correlation ID and request context for tracking
        _logger.LogError(exception, 
            "Unhandled exception occurred. CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method}, QueryString: {QueryString}, RequestBody: {RequestBody}",
            correlationId,
            context.Request.Path,
            context.Request.Method,
            requestContext.QueryString,
            requestContext.Body);

        // Determine the appropriate status code and message
        var (statusCode, title, detail) = exception switch
        {
            ArgumentNullException => (HttpStatusCode.BadRequest, "Invalid Request", "A required parameter was null or missing."),
            ArgumentException argEx => (HttpStatusCode.BadRequest, "Invalid Argument", argEx.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized", "You are not authorized to access this resource."),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Resource Not Found", "The requested resource was not found."),
            InvalidOperationException invEx => (HttpStatusCode.Conflict, "Invalid Operation", invEx.Message),
            TimeoutException => (HttpStatusCode.GatewayTimeout, "Request Timeout", "The operation timed out."),
            OperationCanceledException => (HttpStatusCode.InternalServerError, "Request Cancelled", "The operation was cancelled unexpectedly."),
            NotImplementedException => (HttpStatusCode.NotImplemented, "Not Implemented", "This feature is not yet implemented."),
            // Database-specific exceptions
            DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "Concurrency Conflict", "The record was modified by another user. Please refresh and try again."),
            DbUpdateException dbEx when GetSqlErrorNumber(dbEx) == 2601 || GetSqlErrorNumber(dbEx) == 2627 => 
                (HttpStatusCode.Conflict, "Duplicate Entry", "A record with this key already exists."),
            DbUpdateException dbEx when GetSqlErrorNumber(dbEx) == 547 => 
                (HttpStatusCode.BadRequest, "Reference Constraint", "Cannot delete or modify this record because it is referenced by other records."),
            DbUpdateException => (HttpStatusCode.BadRequest, "Database Error", "An error occurred while saving to the database."),
            SqlException sqlEx when sqlEx.Number == -2 || sqlEx.Number == 53 => 
                (HttpStatusCode.ServiceUnavailable, "Database Unavailable", "The database server is currently unavailable. Please try again later."),
            SqlException => (HttpStatusCode.InternalServerError, "Database Error", "A database error occurred."),
            _ => (HttpStatusCode.InternalServerError, "Internal Server Error", "An unexpected error occurred.")
        };

        // Create Problem Details response (RFC 7807)
        // Never expose raw database exception messages — they can leak connection strings or schema details
        var isDatabaseException = exception is DbUpdateException or SqlException;
        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = _environment.IsDevelopment() && !isDatabaseException ? exception.Message : detail,
            Instance = context.Request.Path,
            Type = $"https://httpstatuses.com/{(int)statusCode}"
        };

        // Add correlation ID for support tracking
        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["timestamp"] = DateTime.UtcNow.ToString("o");

        // In development, include stack trace and type info (but never raw DB messages)
        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
            problemDetails.Extensions["exceptionType"] = exception.GetType().Name;
            
            if (exception.InnerException != null)
            {
                var isInnerDbException = exception.InnerException is DbUpdateException or SqlException;
                problemDetails.Extensions["innerException"] = new
                {
                    message = isInnerDbException ? "Database error details are redacted." : exception.InnerException.Message,
                    type = exception.InnerException.GetType().Name
                };
            }
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        };

        await context.Response.WriteAsJsonAsync(problemDetails, options);
    }

    private static async Task<(string? QueryString, string? Body)> CaptureRequestContextAsync(HttpContext context)
    {
        var queryString = context.Request.QueryString.HasValue
            ? MaskSensitiveData(context.Request.QueryString.Value!)
            : null;

        string? body = null;
        if (context.Request.Body.CanSeek && context.Request.ContentLength is > 0 and <= MaxBodyCaptureBytes)
        {
            context.Request.Body.Position = 0;
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync();
            body = MaskSensitiveData(body);
        }

        return (queryString, body);
    }

    private static string MaskSensitiveData(string value)
    {
        return SensitiveFieldPattern.Replace(value, "$1\"***REDACTED***\"");
    }

    /// <summary>
    /// Extract SQL Server error number from a DbUpdateException
    /// </summary>
    private static int? GetSqlErrorNumber(DbUpdateException ex)
    {
        if (ex.InnerException is SqlException sqlEx)
            return sqlEx.Number;
        return null;
    }
}

/// <summary>
/// Extension methods for registering the global exception middleware.
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
