using ComputerInventory.Core.Entities;

namespace ComputerInventory.Api.Services;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string username, string password);
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task<AuthResult> VerifyEmailAsync(string token);
    Task<AuthResult> ResendVerificationEmailAsync(string email);
    Task<AuthResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
    Task<AuthResult> UpdateProfileAsync(int userId, string firstName, string lastName, string email);
    string GenerateJwtToken(ApplicationUser user);
    Task<(string token, string refreshToken)> CreateTokenPairAsync(ApplicationUser user);
    Task<AuthResult> RefreshTokenAsync(string refreshToken);
    Task RevokeRefreshTokenFamilyAsync(string refreshToken);
}

public class AuthResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public string? Message { get; set; }
    public UserDto? User { get; set; }
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Phone { get; set; }
    public UserRole Role { get; set; } = UserRole.Viewer;
    public string InviteCode { get; set; } = string.Empty;
}

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string Role { get; set; } = string.Empty;
}
