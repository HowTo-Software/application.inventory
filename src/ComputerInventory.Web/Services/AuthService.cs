using ComputerInventory.Contracts;
using System.Net.Http.Json;
using Blazored.LocalStorage;
using ComputerInventory.Web.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace ComputerInventory.Web.Services;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task LogoutAsync();
    Task<UserDto?> GetCurrentUserAsync();
    Task<UserDto?> GetProfileAsync();
    Task<AuthResult> ChangePasswordAsync(ChangePasswordRequest request);
    Task<AuthResult> UpdateProfileAsync(UpdateProfileRequest request);
}

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly AuthTokenProvider _tokenProvider;

    public AuthService(HttpClient httpClient, ILocalStorageService localStorage, AuthenticationStateProvider authStateProvider, AuthTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
        _tokenProvider = tokenProvider;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(ApiEndpoints.Auth.Login, request);
            var result = await response.Content.ReadFromJsonAsync<AuthResult>();

            if (result != null && result.Success && result.User != null)
            {
                // Token is now stored server-side as an httpOnly cookie.
                // We only persist user display info locally.
                _tokenProvider.MarkLoggedIn();
                await _localStorage.SetItemAsync("currentUser", result.User);
                
                if (_authStateProvider is CustomAuthStateProvider customProvider)
                    customProvider.NotifyUserAuthentication(result.User);
            }

            return result ?? new AuthResult { Success = false, Message = "Login failed" };
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, Message = ex.Message };
        }
    }

    public async Task LogoutAsync()
    {
        // Tell server to clear the httpOnly cookie
        try { await _httpClient.PostAsync(ApiEndpoints.Auth.Logout, null); } catch { }

        _tokenProvider.ClearToken();
        await _localStorage.RemoveItemAsync("currentUser");
        
        if (_authStateProvider is CustomAuthStateProvider customProvider)
            customProvider.NotifyUserLogout();
    }

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        return await _localStorage.GetItemAsync<UserDto>("currentUser");
    }

    public async Task<UserDto?> GetProfileAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UserDto>(ApiEndpoints.Auth.Me);
        }
        catch
        {
            return await GetCurrentUserAsync();
        }
    }

    public async Task<AuthResult> ChangePasswordAsync(ChangePasswordRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(ApiEndpoints.Auth.ChangePassword, new
            {
                request.CurrentPassword,
                request.NewPassword
            });
            var result = await response.Content.ReadFromJsonAsync<AuthResult>();
            return result ?? new AuthResult { Success = false, Message = "Failed to change password" };
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, Message = ex.Message };
        }
    }

    public async Task<AuthResult> UpdateProfileAsync(UpdateProfileRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(ApiEndpoints.Auth.UpdateProfile, request);
            var result = await response.Content.ReadFromJsonAsync<AuthResult>();

            if (result != null && result.Success && result.User != null)
            {
                await _localStorage.SetItemAsync("currentUser", result.User);
                if (_authStateProvider is CustomAuthStateProvider customProvider)
                    customProvider.NotifyUserAuthentication(result.User);
            }

            return result ?? new AuthResult { Success = false, Message = "Failed to update profile" };
        }
        catch (Exception ex)
        {
            return new AuthResult { Success = false, Message = ex.Message };
        }
    }
}
