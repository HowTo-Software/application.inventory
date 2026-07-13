using ComputerInventory.Contracts;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace ComputerInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ReferenceDataResolver _resolver;
    private readonly IPasswordValidator _passwordValidator;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUserRepository userRepository, ReferenceDataResolver resolver, IPasswordValidator passwordValidator, ILogger<UsersController> logger)
    {
        _userRepository = userRepository;
        _resolver = resolver;
        _passwordValidator = passwordValidator;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(PagedResult<UserListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserListDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, [FromQuery] string? sortBy = null, [FromQuery] string? sortDirection = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var (items, totalCount) = await _userRepository.GetAllAsync(page, pageSize, search, sortBy, sortDirection, cancellationToken);
            var result = new PagedResult<UserListDto>
            {
                Items = items.Select(MapToListDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all users");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.");
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(id, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }
            return Ok(MapToDetailDto(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.");
        }
    }

    [HttpGet("role/{role}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IEnumerable<UserListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UserListDto>>> GetByRole(UserRole role, CancellationToken cancellationToken)
    {
        try
        {
            var users = await _userRepository.GetByRoleAsync(role, cancellationToken);
            return Ok(users.Select(MapToListDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users by role {Role}", role);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.");
        }
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserListDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserListDto>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var passwordResult = _passwordValidator.Validate(request.Password);
            if (!passwordResult.IsValid)
            {
                return BadRequest(passwordResult.ErrorMessage);
            }

            var (usernameExists, emailExists) = await _userRepository.UserExistsAsync(request.Username, request.Email, cancellationToken);
            if (usernameExists)
            {
                return BadRequest("Username already exists");
            }
            if (emailExists)
            {
                return BadRequest("Email already exists");
            }

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
                IsEmailVerified = true // Admin-created users are pre-verified
            };

            var created = await _userRepository.AddAsync(user, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToListDto(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {Username}", request.Username);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.");
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(id, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }

            user.FirstName = request.FirstName;
            user.LastName = request.LastName;
            user.Email = request.Email;
            user.DepartmentId = await _resolver.ResolveDepartmentIdAsync(request.Department);
            user.Phone = request.Phone;
            user.RoleId = (int)request.Role;
            user.IsActive = request.IsActive;

            await _userRepository.UpdateAsync(user, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.");
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            // Verify the caller's current DB role is still Admin (JWT claim may be stale)
            var callerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (callerIdClaim == null || !int.TryParse(callerIdClaim, out var callerId))
            {
                return Forbid();
            }

            var caller = await _userRepository.GetByIdAsync(callerId, cancellationToken);
            if (caller == null || caller.RoleId != (int)UserRole.Admin)
            {
                return Forbid();
            }

            var user = await _userRepository.GetByIdAsync(id, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }

            // Prevent deleting the last admin
            if (user.RoleId == (int)UserRole.Admin)
            {
                var admins = await _userRepository.GetByRoleAsync(UserRole.Admin, cancellationToken);
                if (admins.Count() <= 1)
                {
                    return BadRequest("Cannot delete the last administrator");
                }
            }

            await _userRepository.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.");
        }
    }

    [HttpPatch("{id}/activate")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(id, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }

            user.IsActive = true;
            await _userRepository.UpdateAsync(user, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating user {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.");
        }
    }

    [HttpPatch("{id}/deactivate")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(id, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }

            user.IsActive = false;
            await _userRepository.UpdateAsync(user, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.");
        }
    }

    [HttpPatch("{id}/reset-password")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(id, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }

            var passwordResult = _passwordValidator.Validate(request.NewPassword);
            if (!passwordResult.IsValid)
            {
                return BadRequest(passwordResult.ErrorMessage);
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _userRepository.UpdateAsync(user, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.");
        }
    }

    private static UserListDto MapToListDto(ApplicationUser user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        FullName = user.FullName,
        Department = user.DepartmentEntity?.Name,
        Role = ((UserRole)user.RoleId).ToString(),
        IsActive = user.IsActive,
        LastLoginAt = user.LastLoginAt
    };

    private static UserDetailDto MapToDetailDto(ApplicationUser user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Department = user.DepartmentEntity?.Name,
        Phone = user.Phone,
        Role = ((UserRole)user.RoleId).ToString(),
        IsActive = user.IsActive,
        CreatedAt = user.CreatedAt,
        LastLoginAt = user.LastLoginAt
    };
}

public class UserListDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class UserDetailDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class CreateUserRequest
{
    [Required, StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
    [Required, MinLength(8), StringLength(128)]
    public string Password { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;
    [StringLength(200)]
    public string? Department { get; set; }
    [StringLength(20)]
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
}

public class UpdateUserRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;
    [StringLength(200)]
    public string? Department { get; set; }
    [StringLength(20)]
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
}

public class ResetPasswordRequest
{
    [Required, MinLength(8), StringLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}
