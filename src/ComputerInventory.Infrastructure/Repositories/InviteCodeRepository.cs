using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ComputerInventory.Infrastructure.Repositories;

public class InviteCodeRepository : IInviteCodeRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<InviteCodeRepository> _logger;

    public InviteCodeRepository(ApplicationDbContext context, ILogger<InviteCodeRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<InviteCode> AddAsync(InviteCode code, CancellationToken cancellationToken = default)
    {
        _context.InviteCodes.Add(code);
        await _context.SaveChangesAsync(cancellationToken);
        return code;
    }

    public async Task<InviteCode?> GetByCodeHashAsync(string codeHash, CancellationToken cancellationToken = default)
    {
        return await _context.InviteCodes
            .FirstOrDefaultAsync(c => c.CodeHash == codeHash, cancellationToken);
    }

    public async Task<InviteCode?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.InviteCodes.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<List<InviteCode>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.InviteCodes
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CleanupExpiredAsync(int maxAgeDays, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays);
        return await _context.InviteCodes
            .Where(c => c.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
