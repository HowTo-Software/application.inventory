using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComputerInventory.Api.Controllers;

/// <summary>
/// Exposes server configuration settings needed by the web UI and agents.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(IConfiguration configuration, ILogger<SettingsController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Get the configured server URL that agents should use to call home.
    /// Falls back to the current request URL if not explicitly configured.
    /// </summary>
    [HttpGet("server-url")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetServerUrl()
    {
        var configuredUrl = _configuration["AgentSettings:ServerUrl"];

        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            // Fall back to deriving from the current request
            configuredUrl = $"{Request.Scheme}://{Request.Host}";
            _logger.LogDebug("No AgentSettings:ServerUrl configured, using request-derived URL: {Url}", configuredUrl);
        }

        return Ok(new
        {
            serverUrl = configuredUrl.TrimEnd('/'),
            isConfigured = !string.IsNullOrWhiteSpace(_configuration["AgentSettings:ServerUrl"])
        });
    }
}
