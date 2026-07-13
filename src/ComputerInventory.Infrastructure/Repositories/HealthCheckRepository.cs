using ComputerInventory.Core.Interfaces;
using ComputerInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ComputerInventory.Infrastructure.Repositories;

public class HealthCheckRepository : IHealthCheckRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HealthCheckRepository> _logger;

    public HealthCheckRepository(ApplicationDbContext context, ILogger<HealthCheckRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> GetHealthCheckCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<int>(
            "sp_health_check", cancellationToken);
    }

    public string GetProviderName()
    {
        return _context.Database.ProviderName ?? "Unknown";
    }
}
