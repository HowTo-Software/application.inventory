using System.Collections.Concurrent;

namespace ComputerInventory.ThinClient.Services;

/// <summary>
/// Manages server connections with automatic failover between multiple URLs.
/// Tries primary URL first, then falls back to configured alternatives.
/// Remembers which URL works and prefers it for subsequent requests.
/// </summary>
public interface IServerConnectionManager
{
    /// <summary>
    /// Gets the current active server URL
    /// </summary>
    string CurrentServerUrl { get; }
    
    /// <summary>
    /// Gets all configured server URLs in priority order
    /// </summary>
    IReadOnlyList<string> ServerUrls { get; }
    
    /// <summary>
    /// Tests connectivity and finds a working server URL
    /// </summary>
    Task<string?> FindWorkingServerAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Marks the current URL as failed and switches to next available
    /// </summary>
    void MarkCurrentUrlFailed();
    
    /// <summary>
    /// Unique session ID for this agent process lifetime, used to correlate requests.
    /// </summary>
    string AgentSessionId { get; }
    
    /// <summary>
    /// Creates an HttpClient configured for the current working server
    /// </summary>
    HttpClient CreateClient();
    
    /// <summary>
    /// Executes an action with automatic URL failover
    /// </summary>
    Task<T?> ExecuteWithFailoverAsync<T>(Func<HttpClient, Task<T>> action, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes an action with automatic URL failover (void return)
    /// </summary>
    Task<bool> ExecuteWithFailoverAsync(Func<HttpClient, Task<bool>> action, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of server connection manager with URL failover support.
/// </summary>
public class ServerConnectionManager : IServerConnectionManager
{
    private readonly ILogger<ServerConnectionManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiKey;
    
    private readonly List<string> _serverUrls = new();
    private readonly ConcurrentDictionary<string, DateTime> _failedUrls = new();
    private readonly TimeSpan _failedUrlCooldown = TimeSpan.FromMinutes(5);
    
    private string _currentUrl = string.Empty;
    private DateTime _lastSuccessfulConnection = DateTime.MinValue;
    private readonly object _urlLock = new();

    /// <summary>
    /// A unique session ID generated once per agent process lifetime.
    /// Sent as X-Agent-Session-Id header on all API calls to correlate
    /// registration, heartbeat, and progress requests from the same session.
    /// </summary>
    public string AgentSessionId { get; } = Guid.NewGuid().ToString("N");

    public string CurrentServerUrl
    {
        get
        {
            lock (_urlLock)
            {
                return _currentUrl;
            }
        }
    }

    public IReadOnlyList<string> ServerUrls => _serverUrls.AsReadOnly();

    public ServerConnectionManager(
        ILogger<ServerConnectionManager> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _apiKey = _configuration["ApiSettings:ApiKey"] ?? string.Empty;
        
        InitializeServerUrls();
    }

    private void InitializeServerUrls()
    {
        // Primary URL from configuration
        var primaryUrl = _configuration["ApiSettings:BaseUrl"];
        if (!string.IsNullOrEmpty(primaryUrl))
        {
            _serverUrls.Add(NormalizeUrl(primaryUrl));
        }
        
        // Fallback URLs from configuration (array of URLs)
        var fallbackUrls = _configuration.GetSection("ApiSettings:FallbackUrls").Get<string[]>();
        if (fallbackUrls != null)
        {
            foreach (var url in fallbackUrls)
            {
                if (!string.IsNullOrEmpty(url))
                {
                    var normalized = NormalizeUrl(url);
                    if (!_serverUrls.Contains(normalized))
                    {
                        _serverUrls.Add(normalized);
                    }
                }
            }
        }
        
        // Default fallback if no URLs configured
        if (_serverUrls.Count == 0)
        {
            _serverUrls.Add("http://localhost:5010");
        }
        
        // Set initial URL
        _currentUrl = _serverUrls[0];
        
        _logger.LogInformation("Server connection manager initialized with {Count} URLs: {Urls}", 
            _serverUrls.Count, string.Join(", ", _serverUrls));
    }

    private static string NormalizeUrl(string url)
    {
        url = url.Trim();
        if (!url.EndsWith("/"))
            url += "/";
        return url;
    }

    public async Task<string?> FindWorkingServerAsync(CancellationToken cancellationToken = default)
    {
        // First try the current URL if it was recently successful
        if (!string.IsNullOrEmpty(_currentUrl) && 
            DateTime.UtcNow - _lastSuccessfulConnection < TimeSpan.FromMinutes(2))
        {
            if (await TestServerAsync(_currentUrl, cancellationToken))
            {
                return _currentUrl;
            }
        }

        // Try all URLs in order
        foreach (var url in _serverUrls)
        {
            // Skip URLs that recently failed (unless all have failed)
            if (_failedUrls.TryGetValue(url, out var failedAt) && 
                DateTime.UtcNow - failedAt < _failedUrlCooldown)
            {
                _logger.LogDebug("Skipping recently failed URL: {Url}", url);
                continue;
            }

            if (await TestServerAsync(url, cancellationToken))
            {
                lock (_urlLock)
                {
                    if (_currentUrl != url)
                    {
                        _logger.LogInformation("Switching to server: {Url}", url);
                        _currentUrl = url;
                    }
                    _lastSuccessfulConnection = DateTime.UtcNow;
                    _failedUrls.TryRemove(url, out _);
                }
                return url;
            }
        }

        // If all URLs failed, try them all again (reset cooldowns)
        _logger.LogWarning("All configured server URLs failed, retrying all...");
        _failedUrls.Clear();
        
        foreach (var url in _serverUrls)
        {
            if (await TestServerAsync(url, cancellationToken))
            {
                lock (_urlLock)
                {
                    _currentUrl = url;
                    _lastSuccessfulConnection = DateTime.UtcNow;
                }
                _logger.LogInformation("Found working server after retry: {Url}", url);
                return url;
            }
        }

        _logger.LogError("No server URLs are reachable");
        return null;
    }

    private async Task<bool> TestServerAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(5) };
            if (!string.IsNullOrEmpty(_apiKey))
            {
                client.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
            }
            
            var response = await client.GetAsync("api/health", cancellationToken);
            var isAvailable = response.IsSuccessStatusCode;
            
            _logger.LogDebug("Server health check for {Url}: {Status}", url, isAvailable ? "OK" : "Failed");
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Server health check failed for {Url}: {Error}", url, ex.Message);
            return false;
        }
    }

    public void MarkCurrentUrlFailed()
    {
        lock (_urlLock)
        {
            if (!string.IsNullOrEmpty(_currentUrl))
            {
                _failedUrls[_currentUrl] = DateTime.UtcNow;
                _logger.LogWarning("Marked server URL as failed: {Url}", _currentUrl);
                
                // Try to switch to next available URL
                var nextUrl = _serverUrls.FirstOrDefault(u => 
                    !_failedUrls.ContainsKey(u) || 
                    DateTime.UtcNow - _failedUrls[u] > _failedUrlCooldown);
                
                if (nextUrl != null && nextUrl != _currentUrl)
                {
                    _logger.LogInformation("Switching to alternate server: {Url}", nextUrl);
                    _currentUrl = nextUrl;
                }
            }
        }
    }

    public HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        
        lock (_urlLock)
        {
            client.BaseAddress = new Uri(_currentUrl);
        }
        
        client.Timeout = TimeSpan.FromSeconds(30);
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            client.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }
        client.DefaultRequestHeaders.Add("X-Agent-Session-Id", AgentSessionId);
        
        return client;
    }

    public async Task<T?> ExecuteWithFailoverAsync<T>(Func<HttpClient, Task<T>> action, CancellationToken cancellationToken = default)
    {
        var triedUrls = new HashSet<string>();
        
        while (triedUrls.Count < _serverUrls.Count)
        {
            var currentUrl = CurrentServerUrl;
            
            if (triedUrls.Contains(currentUrl))
            {
                // Find next URL to try
                var nextUrl = _serverUrls.FirstOrDefault(u => !triedUrls.Contains(u));
                if (nextUrl == null) break;
                
                lock (_urlLock)
                {
                    _currentUrl = nextUrl;
                    currentUrl = nextUrl;
                }
            }
            
            triedUrls.Add(currentUrl);
            
            try
            {
                using var client = CreateClient();
                var result = await action(client);
                
                // Success - update last successful connection
                lock (_urlLock)
                {
                    _lastSuccessfulConnection = DateTime.UtcNow;
                    _failedUrls.TryRemove(currentUrl, out _);
                }
                
                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Request failed for {Url}: {Error}", currentUrl, ex.Message);
                MarkCurrentUrlFailed();
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Request timed out for {Url}", currentUrl);
                MarkCurrentUrlFailed();
            }
        }
        
        _logger.LogError("All server URLs exhausted, operation failed");
        return default;
    }

    public async Task<bool> ExecuteWithFailoverAsync(Func<HttpClient, Task<bool>> action, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteWithFailoverAsync<bool?>(async client => await action(client), cancellationToken);
        return result ?? false;
    }
}
