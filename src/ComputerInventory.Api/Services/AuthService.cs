using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace ComputerInventory.Api.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly ReferenceDataResolver _resolver;
    private readonly IPasswordValidator _passwordValidator;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IInviteCodeService _inviteCodeService;
    private readonly IEmailService _emailService;

    public AuthService(IUserRepository userRepository, IConfiguration configuration, ILogger<AuthService> logger, ReferenceDataResolver resolver, IPasswordValidator passwordValidator, IRefreshTokenRepository refreshTokenRepository, IInviteCodeService inviteCodeService, IEmailService emailService)
    {
        _userRepository = userRepository;
        _configuration = configuration;
        _logger = logger;
        _resolver = resolver;
        _passwordValidator = passwordValidator;
        _refreshTokenRepository = refreshTokenRepository;
        _inviteCodeService = inviteCodeService;
        _emailService = emailService;
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        _logger.LogInformation("Login attempt for user: {Username}", username);
        
        var user = await _userRepository.GetByUsernameAsync(username);
        
        if (user == null)
        {
            _logger.LogWarning("User not found: {Username}", username);
            return new AuthResult { Success = false, Message = "Invalid username or password" };
        }
        
        if (!user.IsActive)
        {
            _logger.LogWarning("User is inactive: {Username}", username);
            return new AuthResult { Success = false, Message = "Invalid username or password" };
        }

        if (!user.IsEmailVerified)
        {
            _logger.LogWarning("User email not verified: {Username}", username);
            return new AuthResult { Success = false, Message = "Please verify your email address before signing in. Check your inbox for a verification link." };
        }

        if (!VerifyPassword(password, user.PasswordHash, user.Username))
        {
            _logger.LogWarning("Invalid password for user: {Username}", username);
            return new AuthResult { Success = false, Message = "Invalid username or password" };
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        var (token, refreshToken) = await CreateTokenPairAsync(user);

        return new AuthResult
        {
            Success = true,
            Token = token,
            RefreshToken = refreshToken,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Department = user.DepartmentEntity?.Name,
                Role = ((UserRole)user.RoleId).ToString()
            }
        };
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InviteCode))
        {
            return new AuthResult { Success = false, Message = "An invite code is required to register" };
        }

        if (!await _inviteCodeService.ValidateAndConsumeAsync(request.InviteCode))
        {
            return new AuthResult { Success = false, Message = "Invalid or expired invite code" };
        }

        var passwordResult = _passwordValidator.Validate(request.Password);
        if (!passwordResult.IsValid)
        {
            return new AuthResult { Success = false, Message = passwordResult.ErrorMessage };
        }

        var (usernameExists, emailExists) = await _userRepository.UserExistsAsync(request.Username, request.Email);
        if (usernameExists)
        {
            return new AuthResult { Success = false, Message = "Username already exists" };
        }
        if (emailExists)
        {
            return new AuthResult { Success = false, Message = "Email already exists" };
        }

        var verificationToken = GenerateVerificationToken();

        var user = new ApplicationUser
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            DepartmentId = await _resolver.ResolveDepartmentIdAsync(request.Department),
            Phone = request.Phone,
            RoleId = (int)request.Role,
            IsActive = true,
            IsEmailVerified = false,
            EmailVerificationToken = HashToken(verificationToken),
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24)
        };

        await _userRepository.AddAsync(user);

        if (_emailService.IsConfigured)
        {
            try
            {
                await _emailService.SendVerificationEmailAsync(user.Email, user.Username, verificationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email during registration for {Username}", user.Username);
            }
        }
        else
        {
            // No email service configured — auto-verify so users aren't locked out
            user.IsEmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiry = null;
            await _userRepository.UpdateAsync(user);
            _logger.LogWarning("Email service not configured — auto-verifying user {Username}", user.Username);
        }

        if (user.IsEmailVerified)
        {
            var (token, refreshToken) = await CreateTokenPairAsync(user);
            return new AuthResult
            {
                Success = true,
                Token = token,
                RefreshToken = refreshToken,
                Message = "User registered successfully",
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Department = user.DepartmentEntity?.Name,
                    Role = ((UserRole)user.RoleId).ToString()
                }
            };
        }

        return new AuthResult
        {
            Success = true,
            Message = "Registration successful. Please check your email to verify your account."
        };
    }

    public async Task<AuthResult> VerifyEmailAsync(string token)
    {
        var tokenHash = HashToken(token);

        var user = await _userRepository.GetByVerificationTokenHashAsync(tokenHash);

        if (user == null)
        {
            return new AuthResult { Success = false, Message = "Invalid verification link" };
        }

        if (user.IsEmailVerified)
        {
            return new AuthResult { Success = true, Message = "Email is already verified. You can sign in." };
        }

        if (user.EmailVerificationTokenExpiry < DateTime.UtcNow)
        {
            return new AuthResult { Success = false, Message = "Verification link has expired. Please request a new one." };
        }

        user.IsEmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiry = null;
        await _userRepository.UpdateAsync(user);

        _logger.LogInformation("Email verified for user {Username}", user.Username);

        return new AuthResult { Success = true, Message = "Email verified successfully. You can now sign in." };
    }

    public async Task<AuthResult> ResendVerificationEmailAsync(string email)
    {
        if (!_emailService.IsConfigured)
        {
            return new AuthResult { Success = false, Message = "Email service is not configured" };
        }

        var user = await _userRepository.GetByEmailAsync(email);

        // Always return success to prevent email enumeration
        if (user == null || user.IsEmailVerified)
        {
            return new AuthResult { Success = true, Message = "If that email is registered and unverified, a new verification link has been sent." };
        }

        var verificationToken = GenerateVerificationToken();
        user.EmailVerificationToken = HashToken(verificationToken);
        user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);
        await _userRepository.UpdateAsync(user);

        try
        {
            await _emailService.SendVerificationEmailAsync(user.Email, user.Username, verificationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resend verification email to {Email}", email);
        }

        return new AuthResult { Success = true, Message = "If that email is registered and unverified, a new verification link has been sent." };
    }

    public async Task<AuthResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return new AuthResult { Success = false, Message = "User not found" };
        }

        if (!VerifyPassword(currentPassword, user.PasswordHash, user.Username))
        {
            return new AuthResult { Success = false, Message = "Current password is incorrect" };
        }

        var passwordResult = _passwordValidator.Validate(newPassword);
        if (!passwordResult.IsValid)
        {
            return new AuthResult { Success = false, Message = passwordResult.ErrorMessage };
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _userRepository.UpdateAsync(user);

        return new AuthResult { Success = true, Message = "Password changed successfully" };
    }

    private bool VerifyPassword(string password, string passwordHash, string username)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BCrypt verification failed for user: {Username}", username);
            return false;
        }
    }

    public async Task<AuthResult> UpdateProfileAsync(int userId, string firstName, string lastName, string email)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return new AuthResult { Success = false, Message = "User not found" };
        }

        // Check if email is taken by another user
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            if (await _userRepository.EmailExistsAsync(email))
            {
                return new AuthResult { Success = false, Message = "Email is already in use by another account" };
            }
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Email = email;
        await _userRepository.UpdateAsync(user);

        // Re-issue tokens so JWT claims reflect updated profile
        var (token, refreshToken) = await CreateTokenPairAsync(user);

        return new AuthResult
        {
            Success = true,
            Message = "Profile updated successfully",
            Token = token,
            RefreshToken = refreshToken,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Department = user.DepartmentEntity?.Name,
                Role = ((UserRole)user.RoleId).ToString()
            }
        };
    }

    public string GenerateJwtToken(ApplicationUser user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]!;
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];
        var expirationMinutes = int.Parse(jwtSettings["ExpirationInMinutes"] ?? "60");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.GivenName, user.FirstName),
            new Claim(ClaimTypes.Surname, user.LastName),
            new Claim(ClaimTypes.Role, ((UserRole)user.RoleId).ToString())
        };

        if (user.DepartmentId.HasValue)
            claims.Add(new Claim("DepartmentId", user.DepartmentId.Value.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<(string token, string refreshToken)> CreateTokenPairAsync(ApplicationUser user)
    {
        var jwt = GenerateJwtToken(user);
        var refreshTokenValue = GenerateRefreshTokenValue();
        var refreshDays = int.Parse(_configuration["JwtSettings:RefreshTokenExpirationInDays"] ?? "7");

        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshTokenValue),
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
            CreatedAt = DateTime.UtcNow,
            Family = Guid.NewGuid().ToString("N")
        };

        await _refreshTokenRepository.AddAsync(entity);

        return (jwt, refreshTokenValue);
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
    {
        var tokenHash = HashToken(refreshToken);

        var stored = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash);

        if (stored == null)
        {
            _logger.LogWarning("Refresh token not found");
            return new AuthResult { Success = false, Message = "Invalid refresh token" };
        }

        // Reuse detection: if the token was already consumed, the family is compromised
        if (stored.ConsumedAt != null)
        {
            _logger.LogWarning("Refresh token reuse detected for family {Family}, user {UserId} — revoking entire family",
                stored.Family, stored.UserId);
            await _refreshTokenRepository.RevokeFamilyAsync(stored.Family);
            return new AuthResult { Success = false, Message = "Invalid refresh token" };
        }

        if (!stored.IsActive)
        {
            _logger.LogWarning("Refresh token expired or revoked for user {UserId}", stored.UserId);
            return new AuthResult { Success = false, Message = "Invalid refresh token" };
        }

        if (!stored.User.IsActive)
        {
            _logger.LogWarning("User {UserId} is inactive — rejecting refresh", stored.UserId);
            return new AuthResult { Success = false, Message = "Invalid refresh token" };
        }

        // Rotate: mark current token as consumed, issue new one in the same family
        stored.ConsumedAt = DateTime.UtcNow;

        var newRefreshTokenValue = GenerateRefreshTokenValue();
        var refreshDays = int.Parse(_configuration["JwtSettings:RefreshTokenExpirationInDays"] ?? "7");

        var newEntity = new RefreshToken
        {
            UserId = stored.UserId,
            TokenHash = HashToken(newRefreshTokenValue),
            ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
            CreatedAt = DateTime.UtcNow,
            Family = stored.Family
        };

        await _refreshTokenRepository.AddAsync(newEntity);

        var user = stored.User;
        var jwt = GenerateJwtToken(user);

        return new AuthResult
        {
            Success = true,
            Token = jwt,
            RefreshToken = newRefreshTokenValue,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Department = user.DepartmentEntity?.Name,
                Role = ((UserRole)user.RoleId).ToString()
            }
        };
    }

    public async Task RevokeRefreshTokenFamilyAsync(string refreshToken)
    {
        var tokenHash = HashToken(refreshToken);
        var stored = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash);

        if (stored != null)
        {
            await _refreshTokenRepository.RevokeFamilyAsync(stored.Family);
        }
    }

    private static string GenerateRefreshTokenValue()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    private static string GenerateVerificationToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    private static string HashToken(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
