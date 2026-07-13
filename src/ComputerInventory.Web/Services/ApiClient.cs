using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Polly;
using Polly.Retry;

namespace ComputerInventory.Web.Services;

/// <summary>
/// Centralized HTTP request handler that provides consistent error handling,
/// status code mapping, and user-friendly error messages.
/// </summary>
public interface IApiClient
{
    Task<ApiResult<T>> GetAsync<T>(string uri, CancellationToken cancellationToken = default);
    Task<ApiResult<T>> PostAsync<T>(string uri, object? content = null, CancellationToken cancellationToken = default);
    Task<ApiResult<T>> PutAsync<T>(string uri, object? content = null, CancellationToken cancellationToken = default);
    Task<ApiResult<T>> PatchAsync<T>(string uri, object? content = null, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAsync(string uri, CancellationToken cancellationToken = default);
    Task<ApiResult> PostAsync(string uri, object? content = null, CancellationToken cancellationToken = default);
    Task<ApiResult> PutAsync(string uri, object? content = null, CancellationToken cancellationToken = default);
    Task<ApiResult> PatchAsync(string uri, object? content = null, CancellationToken cancellationToken = default);
}

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClient> _logger;
    private readonly AuthTokenProvider _tokenProvider;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger, AuthTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _logger = logger;
        _tokenProvider = tokenProvider;

        _resiliencePipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                    .HandleResult(r => IsTransientStatusCode(r.StatusCode)),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retry attempt {AttemptNumber} after {Delay}ms. Reason: {Reason}",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? $"HTTP {(int?)args.Outcome.Result?.StatusCode}");
                    return default;
                }
            })
            .Build();
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    public async Task<ApiResult<T>> GetAsync<T>(string uri, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync<T>(
            () => _httpClient.GetAsync(uri, cancellationToken),
            "GET", uri);
    }

    public async Task<ApiResult<T>> PostAsync<T>(string uri, object? content = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync<T>(
            () => content != null 
                ? _httpClient.PostAsJsonAsync(uri, content, cancellationToken)
                : _httpClient.PostAsync(uri, null, cancellationToken),
            "POST", uri);
    }

    public async Task<ApiResult<T>> PutAsync<T>(string uri, object? content = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync<T>(
            () => content != null
                ? _httpClient.PutAsJsonAsync(uri, content, cancellationToken)
                : _httpClient.PutAsync(uri, null, cancellationToken),
            "PUT", uri);
    }

    public async Task<ApiResult<T>> PatchAsync<T>(string uri, object? content = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync<T>(
            () => _httpClient.PatchAsJsonAsync(uri, content, cancellationToken),
            "PATCH", uri);
    }

    public async Task<ApiResult> DeleteAsync(string uri, CancellationToken cancellationToken = default)
    {
        return ToNonGeneric(await ExecuteAsync<object>(
            () => _httpClient.DeleteAsync(uri, cancellationToken),
            "DELETE", uri, deserializeResponse: false));
    }

    public async Task<ApiResult> PostAsync(string uri, object? content = null, CancellationToken cancellationToken = default)
    {
        return ToNonGeneric(await ExecuteAsync<object>(
            () => content != null
                ? _httpClient.PostAsJsonAsync(uri, content, cancellationToken)
                : _httpClient.PostAsync(uri, null, cancellationToken),
            "POST", uri, deserializeResponse: false));
    }

    public async Task<ApiResult> PutAsync(string uri, object? content = null, CancellationToken cancellationToken = default)
    {
        return ToNonGeneric(await ExecuteAsync<object>(
            () => content != null
                ? _httpClient.PutAsJsonAsync(uri, content, cancellationToken)
                : _httpClient.PutAsync(uri, null, cancellationToken),
            "PUT", uri, deserializeResponse: false));
    }

    public async Task<ApiResult> PatchAsync(string uri, object? content = null, CancellationToken cancellationToken = default)
    {
        return ToNonGeneric(await ExecuteAsync<object>(
            () => _httpClient.PatchAsJsonAsync(uri, content, cancellationToken),
            "PATCH", uri, deserializeResponse: false));
    }

    private async Task<ApiResult<T>> ExecuteAsync<T>(
        Func<Task<HttpResponseMessage>> operation, 
        string method, 
        string uri,
        bool deserializeResponse = true)
    {
        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(
                async _ => await operation(),
                CancellationToken.None);
            var correlationId = GetCorrelationId(response);

            if (response.IsSuccessStatusCode)
            {
                if (!deserializeResponse)
                    return new ApiResult<T> { Success = true };

                try
                {
                    var data = await response.Content.ReadFromJsonAsync<T>(_jsonOptions);
                    return ApiResult<T>.Ok(data!);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize response from {Method} {Uri}", method, uri);
                    return ApiResult<T>.Fail(
                        "Received invalid data from the server.",
                        (int)response.StatusCode,
                        ex.Message,
                        correlationId);
                }
            }

            return await HandleErrorResponse<T>(response, method, uri, correlationId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during {Method} {Uri}", method, uri);
            return ApiResult<T>.NetworkError(ex.Message);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Request cancelled: {Method} {Uri}", method, uri);
            return ApiResult<T>.Fail("The request was cancelled.");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Request timeout: {Method} {Uri}", method, uri);
            return ApiResult<T>.Fail(
                "The request timed out. Please try again.",
                408,
                ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during {Method} {Uri}", method, uri);
            return ApiResult<T>.ServerError(details: ex.Message);
        }
    }

    private async Task<ApiResult<T>> HandleErrorResponse<T>(
        HttpResponseMessage response, 
        string method, 
        string uri,
        string? correlationId)
    {
        var statusCode = (int)response.StatusCode;
        var problemDetails = await TryReadProblemDetails(response);

        _logger.LogWarning(
            "API error: {Method} {Uri} returned {StatusCode}. CorrelationId: {CorrelationId}. Detail: {Detail}",
            method, uri, statusCode, correlationId, problemDetails?.Detail);

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => ApiResult<T>.Unauthorized(),
            HttpStatusCode.Forbidden => ApiResult<T>.Forbidden(
                problemDetails?.Detail ?? "You don't have permission to perform this action."),
            HttpStatusCode.NotFound => ApiResult<T>.NotFound(
                problemDetails?.Detail ?? "The requested resource was not found."),
            HttpStatusCode.BadRequest => ApiResult<T>.Fail(
                problemDetails?.Detail ?? "The request was invalid. Please check your input.",
                statusCode,
                correlationId: correlationId),
            HttpStatusCode.Conflict => ApiResult<T>.Fail(
                problemDetails?.Detail ?? "The operation conflicts with existing data. Please refresh and try again.",
                statusCode,
                correlationId: correlationId),
            HttpStatusCode.TooManyRequests => ApiResult<T>.Fail(
                "Too many requests. Please wait a moment and try again.",
                statusCode,
                correlationId: correlationId),
            HttpStatusCode.ServiceUnavailable => ApiResult<T>.Fail(
                "The service is temporarily unavailable. Please try again later.",
                statusCode,
                correlationId: correlationId),
            HttpStatusCode.GatewayTimeout => ApiResult<T>.Fail(
                "The server took too long to respond. Please try again.",
                statusCode,
                correlationId: correlationId),
            _ when statusCode >= 500 => ApiResult<T>.ServerError(correlationId, problemDetails?.Detail),
            _ => ApiResult<T>.Fail(
                problemDetails?.Detail ?? $"An error occurred (Status: {statusCode}).",
                statusCode,
                correlationId: correlationId)
        };
    }

    private static ApiResult ToNonGeneric(ApiResult<object> result) => new()
    {
        Success = result.Success,
        Data = result.Data,
        ErrorMessage = result.ErrorMessage,
        ErrorDetails = result.ErrorDetails,
        StatusCode = result.StatusCode,
        RequiresAuthentication = result.RequiresAuthentication,
        CorrelationId = result.CorrelationId
    };

    private static string? GetCorrelationId(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("X-Correlation-Id", out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private static async Task<ProblemDetails?> TryReadProblemDetails(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content))
                return null;

            var details = JsonSerializer.Deserialize<ProblemDetails>(content, _jsonOptions);

            // If Detail is empty but we have validation errors, build a message from them
            if (details != null && string.IsNullOrEmpty(details.Detail) && details.Errors is { Count: > 0 })
            {
                var messages = details.Errors
                    .SelectMany(e => e.Value.Select(msg => $"{e.Key}: {msg}"))
                    .ToList();
                details.Detail = string.Join("; ", messages);
            }

            // Fall back to plain string responses (e.g. BadRequest("some message"))
            if (details?.Status == null && !string.IsNullOrWhiteSpace(content))
            {
                var plain = content.Trim().Trim('"');
                if (!plain.StartsWith('{'))
                    return new ProblemDetails { Detail = plain };
            }

            return details;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Represents RFC 7807 Problem Details (including validation errors)
    /// </summary>
    private class ProblemDetails
    {
        public string? Type { get; set; }
        public string? Title { get; set; }
        public int? Status { get; set; }
        public string? Detail { get; set; }
        public string? Instance { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
