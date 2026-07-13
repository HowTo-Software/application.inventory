namespace ComputerInventory.Web.Services;

/// <summary>
/// Standardized wrapper for API responses that provides consistent error handling.
/// Use this to communicate success/failure and error details to the UI.
/// </summary>
/// <typeparam name="T">The type of data returned on success</typeparam>
public class ApiResult<T>
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// The data returned from the API (null if failed)
    /// </summary>
    public T? Data { get; init; }
    
    /// <summary>
    /// User-friendly error message to display
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Technical error details for debugging/logging
    /// </summary>
    public string? ErrorDetails { get; init; }
    
    /// <summary>
    /// HTTP status code from the response (if applicable)
    /// </summary>
    public int? StatusCode { get; init; }
    
    /// <summary>
    /// Indicates if the error requires user re-authentication
    /// </summary>
    public bool RequiresAuthentication { get; init; }
    
    /// <summary>
    /// Correlation ID from the API for tracking/support
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Create a successful result with data
    /// </summary>
    public static ApiResult<T> Ok(T data) => new()
    {
        Success = true,
        Data = data
    };

    /// <summary>
    /// Create a failed result with error information
    /// </summary>
    public static ApiResult<T> Fail(string message, int? statusCode = null, string? details = null, string? correlationId = null) => new()
    {
        Success = false,
        ErrorMessage = message,
        StatusCode = statusCode,
        ErrorDetails = details,
        CorrelationId = correlationId,
        RequiresAuthentication = statusCode == 401
    };

    /// <summary>
    /// Create a failed result for authentication errors
    /// </summary>
    public static ApiResult<T> Unauthorized(string message = "Your session has expired. Please log in again.") => new()
    {
        Success = false,
        ErrorMessage = message,
        StatusCode = 401,
        RequiresAuthentication = true
    };

    /// <summary>
    /// Create a failed result for forbidden access
    /// </summary>
    public static ApiResult<T> Forbidden(string message = "You don't have permission to perform this action.") => new()
    {
        Success = false,
        ErrorMessage = message,
        StatusCode = 403
    };

    /// <summary>
    /// Create a failed result for not found errors
    /// </summary>
    public static ApiResult<T> NotFound(string message = "The requested resource was not found.") => new()
    {
        Success = false,
        ErrorMessage = message,
        StatusCode = 404
    };

    /// <summary>
    /// Create a failed result for network/connection errors
    /// </summary>
    public static ApiResult<T> NetworkError(string? details = null) => new()
    {
        Success = false,
        ErrorMessage = "Unable to connect to the server. Please check your network connection.",
        ErrorDetails = details
    };

    /// <summary>
    /// Create a failed result for server errors
    /// </summary>
    public static ApiResult<T> ServerError(string? correlationId = null, string? details = null) => new()
    {
        Success = false,
        ErrorMessage = "An unexpected server error occurred. Please try again later.",
        StatusCode = 500,
        CorrelationId = correlationId,
        ErrorDetails = details
    };
}

/// <summary>
/// Non-generic version for operations that don't return data
/// </summary>
public class ApiResult : ApiResult<object>
{
    /// <summary>
    /// Create a successful result without data
    /// </summary>
    public static ApiResult Ok() => new()
    {
        Success = true
    };

    /// <summary>
    /// Create a failed result with error information
    /// </summary>
    public new static ApiResult Fail(string message, int? statusCode = null, string? details = null, string? correlationId = null) => new()
    {
        Success = false,
        ErrorMessage = message,
        StatusCode = statusCode,
        ErrorDetails = details,
        CorrelationId = correlationId,
        RequiresAuthentication = statusCode == 401
    };

    /// <summary>
    /// Create a failed result for authentication errors
    /// </summary>
    public new static ApiResult Unauthorized(string message = "Your session has expired. Please log in again.") => new()
    {
        Success = false,
        ErrorMessage = message,
        StatusCode = 401,
        RequiresAuthentication = true
    };

    /// <summary>
    /// Create a failed result for forbidden access
    /// </summary>
    public new static ApiResult Forbidden(string message = "You don't have permission to perform this action.") => new()
    {
        Success = false,
        ErrorMessage = message,
        StatusCode = 403
    };

    /// <summary>
    /// Create a failed result for not found errors
    /// </summary>
    public new static ApiResult NotFound(string message = "The requested resource was not found.") => new()
    {
        Success = false,
        ErrorMessage = message,
        StatusCode = 404
    };

    /// <summary>
    /// Create a failed result for network/connection errors
    /// </summary>
    public new static ApiResult NetworkError(string? details = null) => new()
    {
        Success = false,
        ErrorMessage = "Unable to connect to the server. Please check your network connection.",
        ErrorDetails = details
    };

    /// <summary>
    /// Create a failed result for server errors
    /// </summary>
    public new static ApiResult ServerError(string? correlationId = null, string? details = null) => new()
    {
        Success = false,
        ErrorMessage = "An unexpected server error occurred. Please try again later.",
        StatusCode = 500,
        CorrelationId = correlationId,
        ErrorDetails = details
    };
}
