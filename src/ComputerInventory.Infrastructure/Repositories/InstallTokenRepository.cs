using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ComputerInventory.Infrastructure.Repositories;

public class InstallTokenRepository : IInstallTokenRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<InstallTokenRepository> _logger;

    public InstallTokenRepository(ApplicationDbContext context, ILogger<InstallTokenRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<InstallToken> AddAsync(InstallToken token, CancellationToken cancellationToken = default)
    {
        _context.InstallTokens.Add(token);
        await _context.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<InstallToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await _context.InstallTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CleanupExpiredAsync(int maxAgeDays, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays);
        return await _context.InstallTokens
            .Where(t => t.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
