using System.Net;
using ComputerInventory.Contracts;

namespace ComputerInventory.Web.Services;

/// <summary>
/// HTTP message handler that transparently refreshes expired access tokens.
/// When a request receives a 401 response, this handler calls the refresh endpoint
/// (which reads the httpOnly refresh_token cookie) to obtain a new access_token cookie,
/// then retries the original request exactly once.
/// </summary>
public class AuthorizationMessageHandler : DelegatingHandler
{
    private readonly AuthTokenProvider _tokenProvider;
    private static readonly SemaphoreSlim _refreshLock = new(1, 1);
    private static DateTime _lastRefreshAttempt = DateTime.MinValue;

    public AuthorizationMessageHandler(AuthTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        // Don't try to refresh if:
        //  - the response isn't 401
        //  - the request was itself a refresh or login call (avoid loops)
        //  - the user isn't logged in
        if (response.StatusCode != HttpStatusCode.Unauthorized
            || !_tokenProvider.HasToken
            || IsAuthEndpoint(request.RequestUri))
        {
            return response;
        }

        // Attempt a single silent refresh
        if (await TryRefreshAsync(cancellationToken))
        {
            // Retry the original request with the new cookie
            var retry = await CloneAndResend(request, cancellationToken);
            if (retry.StatusCode != HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                return retry;
            }
            retry.Dispose();
        }

        // Refresh failed — signal session expiry so the UI can react
        _tokenProvider.NotifyExpired();
        return response;
    }

    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        // Prevent concurrent refresh storms
        if (!await _refreshLock.WaitAsync(0, cancellationToken))
        {
            // Another refresh is in progress — wait for it, then assume the cookie is updated
            await _refreshLock.WaitAsync(cancellationToken);
            _refreshLock.Release();
            return _tokenProvider.HasToken;
        }

        try
        {
            // Debounce: don't hammer refresh if we just tried
            if ((DateTime.UtcNow - _lastRefreshAttempt).TotalSeconds < 5)
                return false;

            _lastRefreshAttempt = DateTime.UtcNow;

            var refreshRequest = new HttpRequestMessage(HttpMethod.Post, ApiEndpoints.Auth.Refresh);
            var response = await base.SendAsync(refreshRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _tokenProvider.MarkRefreshed();
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<HttpResponseMessage> CloneAndResend(HttpRequestMessage original, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (original.Content != null)
        {
            var body = await original.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(body);
            if (original.Content.Headers.ContentType != null)
                clone.Content.Headers.ContentType = original.Content.Headers.ContentType;
        }

        return await base.SendAsync(clone, cancellationToken);
    }

    private static bool IsAuthEndpoint(Uri? uri)
    {
        if (uri == null) return false;
        var path = uri.PathAndQuery;
        return path.Contains("/auth/refresh", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/auth/logout", StringComparison.OrdinalIgnoreCase);
    }
}
