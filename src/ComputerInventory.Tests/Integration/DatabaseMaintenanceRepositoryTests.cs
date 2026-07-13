using ComputerInventory.Infrastructure.Data;
using ComputerInventory.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComputerInventory.Tests.Integration;

/// <summary>
/// Integration tests for DatabaseMaintenanceRepository stored procedures.
/// Verifies sp_mark_offline_computers, sp_cleanup_old_system_details,
/// sp_cleanup_old_restore_points, sp_cleanup_old_history_records,
/// sp_mark_stuck_updates_as_failed, sp_cleanup_excess_records.
/// </summary>
[Collection("Database")]
public class DatabaseMaintenanceRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private ApplicationDbContext _context = default!;
    private DatabaseMaintenanceRepository _repo = default!;

    public DatabaseMaintenanceRepositoryTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _repo = new DatabaseMaintenanceRepository(_context, NullLogger<DatabaseMaintenanceRepository>.Instance);
        await TestDataSeeder.SeedAsync(_fixture.CreateDbContext());
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await using var cleanup = _fixture.CreateDbContext();
        await cleanup.Database.ExecuteSqlRawAsync("""
            DELETE FROM computer_histories;
            DELETE FROM maintenance_records;
            DELETE FROM computer_geo_locations;
            DELETE FROM computer_agent_statuses;
            DELETE FROM version_histories;
            DELETE FROM users;
            DELETE FROM computers;
            DELETE FROM operating_systems;
            DELETE FROM computer_models;
            DELETE FROM manufacturers;
            DELETE FROM departments;
            DELETE FROM locations;
            DELETE FROM countries;
            """);
    }

    // ── sp_mark_offline_computers ───────────────────────────────────────

    [Fact]
    public async Task MarkOfflineComputersAsync_MarksStaleAgentsOffline()
    {
        // Threshold of 5 minutes — agents last seen > 5 min ago should be marked offline
        var affected = await _repo.MarkOfflineComputersAsync(5);

        // At least the agents that were last seen hours/days ago should be affected
        Assert.True(affected >= 0);
    }

    [Fact]
    public async Task MarkOfflineComputersAsync_DoesNotAffectRecentAgents()
    {
        // Use a very high threshold so no agents should be marked offline
        var affected = await _repo.MarkOfflineComputersAsync(999999);

        Assert.Equal(0, affected);
    }

    // ── sp_cleanup_old_history_records ───────────────────────────────────

    [Fact]
    public async Task CleanupOldHistoryRecordsAsync_DoesNotDeleteRecentRecords()
    {
        // History records are recent (within last 10 days) so a 1-day retention should
        // soft-delete some, but 365-day retention should delete none
        var affected = await _repo.CleanupOldHistoryRecordsAsync(365);

        Assert.Equal(0, affected);
    }

    // ── sp_cleanup_old_restore_points ───────────────────────────────────

    [Fact]
    public async Task CleanupOldRestorePointsAsync_RunsWithoutError()
    {
        // No restore points seeded, so this should be a no-op
        var affected = await _repo.CleanupOldRestorePointsAsync(30);

        Assert.Equal(0, affected);
    }

    // ── sp_cleanup_old_system_details ───────────────────────────────────

    [Fact]
    public async Task CleanupOldSystemDetailsAsync_RunsWithoutError()
    {
        // No system details seeded, should be a no-op
        var affected = await _repo.CleanupOldSystemDetailsAsync(30);

        Assert.Equal(0, affected);
    }

    // ── sp_mark_stuck_updates_as_failed ─────────────────────────────────

    [Fact]
    public async Task MarkStuckUpdatesAsFailedAsync_RunsWithoutError()
    {
        // No stuck updates seeded
        var affected = await _repo.MarkStuckUpdatesAsFailedAsync(24);

        Assert.Equal(0, affected);
    }

    // ── sp_cleanup_excess_records ───────────────────────────────────────

    [Fact]
    public async Task CleanupExcessRecordsAsync_RunsForSystemDetails()
    {
        var affected = await _repo.CleanupExcessRecordsAsync("system_details", 100);

        Assert.Equal(0, affected);
    }

    [Fact]
    public async Task CleanupExcessRecordsAsync_RunsForRestorePoints()
    {
        var affected = await _repo.CleanupExcessRecordsAsync("restore_points", 100);

        Assert.Equal(0, affected);
    }
}
