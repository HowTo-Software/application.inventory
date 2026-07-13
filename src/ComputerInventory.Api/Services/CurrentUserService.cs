using ComputerInventory.Core.Interfaces;

namespace ComputerInventory.Api.Services;

/// <summary>
/// Resolves the current authenticated user from HttpContext.
/// Returns null for unauthenticated requests (e.g. agent API-key auth).
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Username => _httpContextAccessor.HttpContext?.User?.Identity?.Name;
}
