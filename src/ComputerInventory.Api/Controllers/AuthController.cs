using ComputerInventory.Api.Middleware;
using ComputerInventory.Api.Services;
using ComputerInventory.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace ComputerInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;
    private readonly IInviteCodeService _inviteCodeService;
    private readonly ILoginRateLimiter _loginRateLimiter;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, IConfiguration configuration, IInviteCodeService inviteCodeService, ILoginRateLimiter loginRateLimiter, ILogger<AuthController> logger)
    {
        _authService = authService;
        _configuration = configuration;
        _inviteCodeService = inviteCodeService;
        _loginRateLimiter = loginRateLimiter;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResult>> Login([FromBody] LoginRequest request)
    {
        if (_loginRateLimiter.IsLockedOut(request.Username, out var retryAfter))
        {
            Response.Headers["Retry-After"] = retryAfter.ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new AuthResult { Success = false, Message = $"Too many failed attempts. Try again in {retryAfter} seconds." });
        }

        try
        {
            var result = await _authService.LoginAsync(request.Username, request.Password);

            if (!result.Success)
            {
                _loginRateLimiter.RecordFailure(request.Username);
                return Unauthorized(result);
            }

            _loginRateLimiter.RecordSuccess(request.Username);
            SetAuthCookie(result.Token!);
            SetRefreshCookie(result.RefreshToken!);
            result.Token = null;
            result.RefreshToken = null;

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user {Username}", request.Username);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new AuthResult { Success = false, Message = "An unexpected error occurred. Please try again later." });
        }
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResult>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _authService.RegisterAsync(request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            // Only set auth cookies if registration resulted in immediate login
            // (email verification disabled or auto-verified)
            if (result.Token != null)
            {
                SetAuthCookie(result.Token);
                SetRefreshCookie(result.RefreshToken!);
                result.Token = null;
                result.RefreshToken = null;
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for user {Username}", request.Username);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new AuthResult { Success = false, Message = "An unexpected error occurred. Please try again later." });
        }
    }

    [AllowAnonymous]
    [HttpGet("verify-email")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResult>> VerifyEmail([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new AuthResult { Success = false, Message = "Verification token is required" });
        }

        try
        {
            var result = await _authService.VerifyEmailAsync(token);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during email verification");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new AuthResult { Success = false, Message = "An unexpected error occurred. Please try again later." });
        }
    }

    [AllowAnonymous]
    [HttpPost("resend-verification")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResult>> ResendVerificationEmail([FromBody] ResendVerificationRequest request)
    {
        try
        {
            var result = await _authService.ResendVerificationEmailAsync(request.Email);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending verification email");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new AuthResult { Success = false, Message = "An unexpected error occurred. Please try again later." });
        }
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResult>> Refresh()
    {
        var refreshToken = Request.Cookies["refresh_token"];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Unauthorized(new AuthResult { Success = false, Message = "No refresh token" });
        }

        try
        {
            var result = await _authService.RefreshTokenAsync(refreshToken);
            if (!result.Success)
            {
                DeleteRefreshCookie();
                return Unauthorized(result);
            }

            SetAuthCookie(result.Token!);
            SetRefreshCookie(result.RefreshToken!);
            result.Token = null;
            result.RefreshToken = null;

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new AuthResult { Success = false, Message = "An unexpected error occurred. Please try again later." });
        }
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        try
        {
            // Revoke the refresh token family server-side
            var refreshToken = Request.Cookies["refresh_token"];
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _authService.RevokeRefreshTokenFamilyAsync(refreshToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking refresh token during logout");
        }

        Response.Cookies.Delete("access_token", new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
        DeleteRefreshCookie();
        return Ok();
    }

    /// <summary>
    /// Validates the caller's JWT. The [Authorize] attribute handles signature
    /// and expiration verification via the configured JWT bearer middleware.
    /// Returns 200 with user info if valid, 401 if the token is invalid/expired.
    /// </summary>
    [Authorize]
    [HttpPost("validate")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult ValidateToken()
    {
        return Ok(GetCurrentUserDto());
    }

    /// <summary>
    /// Returns the current authenticated user's profile extracted from JWT claims.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetMe()
    {
        return Ok(GetCurrentUserDto());
    }

    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResult>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
        if (userId == 0)
            return Unauthorized();

        try
        {
            var result = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new AuthResult { Success = false, Message = "An unexpected error occurred. Please try again later." });
        }
    }

    [Authorize]
    [HttpPut("profile")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResult>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
        if (userId == 0)
            return Unauthorized();

        try
        {
            var result = await _authService.UpdateProfileAsync(userId, request.FirstName, request.LastName, request.Email);
            if (!result.Success)
                return BadRequest(result);

            // Re-issue cookies with new JWT containing updated claims
            if (result.Token != null)
            {
                SetAuthCookie(result.Token);
                result.Token = null;
            }
            if (result.RefreshToken != null)
            {
                SetRefreshCookie(result.RefreshToken);
                result.RefreshToken = null;
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new AuthResult { Success = false, Message = "An unexpected error occurred. Please try again later." });
        }
    }

    // ================================================================
    // INVITE CODE MANAGEMENT (Admin only)
    // ================================================================

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("invite-codes")]
    [ProducesResponseType(typeof(InviteCodeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InviteCodeResponse>> CreateInviteCode([FromBody] CreateInviteCodeRequest request)
    {
        try
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value ?? "unknown";
            var (rawCode, entity) = await _inviteCodeService.CreateCodeAsync(
                username, request.MaxUses, request.ExpirationHours, request.Note);

            return Ok(new InviteCodeResponse
            {
                Id = entity.Id,
                Code = rawCode,
                MaxUses = entity.MaxUses,
                ExpiresAt = entity.ExpiresAt,
                Note = entity.Note
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating invite code");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("invite-codes")]
    [ProducesResponseType(typeof(List<InviteCodeListItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<InviteCodeListItem>>> GetInviteCodes()
    {
        try
        {
            var codes = await _inviteCodeService.GetAllAsync();
            return Ok(codes.Select(c => new InviteCodeListItem
            {
                Id = c.Id,
                MaxUses = c.MaxUses,
                UseCount = c.UseCount,
                ExpiresAt = c.ExpiresAt,
                CreatedAt = c.CreatedAt,
                CreatedBy = c.CreatedBy,
                Note = c.Note,
                IsRevoked = c.IsRevoked,
                IsValid = c.IsValid
            }).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invite codes");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("invite-codes/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeInviteCode(int id)
    {
        try
        {
            var revoked = await _inviteCodeService.RevokeAsync(id);
            if (!revoked) return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking invite code {InviteCodeId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    private UserDto GetCurrentUserDto()
    {
        return new UserDto
        {
            Id = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0,
            Username = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
            FirstName = User.FindFirst(ClaimTypes.GivenName)?.Value ?? string.Empty,
            LastName = User.FindFirst(ClaimTypes.Surname)?.Value ?? string.Empty,
            Role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty
        };
    }

    private void SetAuthCookie(string token)
    {
        var expirationMinutes = int.Parse(
            _configuration["JwtSettings:ExpirationInMinutes"] ?? "60");

        Response.Cookies.Append("access_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes)
        });
    }

    private void SetRefreshCookie(string refreshToken)
    {
        var refreshDays = int.Parse(
            _configuration["JwtSettings:RefreshTokenExpirationInDays"] ?? "7");

        Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",  // Only sent to auth endpoints
            Expires = DateTimeOffset.UtcNow.AddDays(refreshDays)
        });
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete("refresh_token", new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth"
        });
    }
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;
    [Required]
    public string NewPassword { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
}

public class ResendVerificationRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}
