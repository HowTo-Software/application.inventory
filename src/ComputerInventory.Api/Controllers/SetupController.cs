using System.ComponentModel.DataAnnotations;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComputerInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class SetupController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<SetupController> _logger;

    public SetupController(IUserRepository userRepository, ILogger<SetupController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Check whether first-run setup is required (no users exist yet).
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(SetupStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SetupStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var (_, totalCount) = await _userRepository.GetAllAsync(1, 1, cancellationToken: cancellationToken);
        var needsSetup = totalCount == 0;

        return Ok(new SetupStatusResponse { SetupRequired = needsSetup });
    }

    /// <summary>
    /// Create the initial administrator account. Only works when no users exist.
    /// </summary>
    [HttpPost("initialize")]
    [ProducesResponseType(typeof(SetupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SetupResult), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SetupResult>> Initialize([FromBody] InitialSetupRequest request, CancellationToken cancellationToken)
    {
        // Verify no users exist — this endpoint is only valid during first-run
        var (_, userCount) = await _userRepository.GetAllAsync(1, 1, cancellationToken: cancellationToken);
        if (userCount > 0)
        {
            _logger.LogWarning("Setup initialize called but users already exist — rejected");
            return BadRequest(new SetupResult
            {
                Success = false,
                Message = "Setup has already been completed. An administrator account already exists."
            });
        }

        // Validate password length
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return BadRequest(new SetupResult
            {
                Success = false,
                Message = "Password must be at least 8 characters long."
            });
        }

        var adminUser = new ApplicationUser
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            RoleId = (int)UserRole.Admin,
            IsActive = true,
            IsEmailVerified = true, // First admin is pre-verified
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(adminUser, cancellationToken);

        _logger.LogInformation("First-run setup completed — admin account '{Username}' created", adminUser.Username);

        return Ok(new SetupResult
        {
            Success = true,
            Message = "Administrator account created successfully. You can now sign in."
        });
    }
}

public class SetupStatusResponse
{
    public bool SetupRequired { get; set; }
}

public class InitialSetupRequest
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;
}

public class SetupResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}
