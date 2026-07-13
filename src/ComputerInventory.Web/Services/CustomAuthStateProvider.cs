using System.Net.Http.Json;
using System.Security.Claims;
using Blazored.LocalStorage;
using ComputerInventory.Contracts;
using ComputerInventory.Web.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace ComputerInventory.Web.Services;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private readonly HttpClient _httpClient;
    private readonly AuthTokenProvider _tokenProvider;
    private readonly ILogger<CustomAuthStateProvider> _logger;
    private static readonly AuthenticationState AnonymousState =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public CustomAuthStateProvider(ILocalStorageService localStorage, HttpClient httpClient,
        AuthTokenProvider tokenProvider, ILogger<CustomAuthStateProvider> logger)
    {
        _localStorage = localStorage;
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Check if we have stored user info (indicates a previous login)
        var storedUser = await _localStorage.GetItemAsync<UserDto>("currentUser");
        if (storedUser == null)
        {
            return AnonymousState;
        }

        // Validate the httpOnly cookie with the server — cookie is sent automatically
        var serverUser = await ValidateWithServerAsync();
        if (serverUser == null)
        {
            _logger.LogWarning("Auth cookie rejected by server — clearing local state");
            await ClearLocalStateAsync();
            return AnonymousState;
        }

        // Update stored user info with server response (source of truth)
        await _localStorage.SetItemAsync("currentUser", serverUser);
        _tokenProvider.MarkLoggedIn();

        return BuildAuthState(serverUser);
    }

    public void NotifyUserAuthentication(UserDto user)
    {
        NotifyAuthenticationStateChanged(Task.FromResult(BuildAuthState(user)));
    }

    public void NotifyUserLogout()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(AnonymousState));
    }

    /// <summary>
    /// Validates the httpOnly auth cookie by calling the server's validate endpoint.
    /// Returns the user info from server claims, or null if invalid/unreachable.
    /// </summary>
    private async Task<UserDto?> ValidateWithServerAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync(ApiEndpoints.Auth.Validate, null);
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<UserDto>();
        }
        catch (Exception ex)
        {
            // Network failure — deny access rather than trusting cached credentials,
            // which may have been revoked server-side.
            _logger.LogWarning(ex, "Could not reach server for cookie validation — denying access");
            return null;
        }
    }

    private async Task ClearLocalStateAsync()
    {
        _tokenProvider.ClearToken();
        await _localStorage.RemoveItemAsync("currentUser");
    }

    private static AuthenticationState BuildAuthState(UserDto user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new(ClaimTypes.Role, user.Role)
        };

        var identity = new ClaimsIdentity(claims, "cookie");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}
