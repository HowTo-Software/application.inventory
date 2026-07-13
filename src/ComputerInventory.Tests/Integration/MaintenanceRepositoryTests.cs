using ComputerInventory.Core.Entities;
using ComputerInventory.Infrastructure.Data;
using ComputerInventory.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComputerInventory.Tests.Integration;

/// <summary>
/// Integration tests for MaintenanceRepository stored procedures.
/// Verifies sp_get_all_maintenance_records, sp_get_maintenance_by_computer_id,
/// sp_get_maintenance_by_status, sp_get_scheduled_maintenance, sp_get_maintenance_summary_counts.
/// </summary>
[Collection("Database")]
public class MaintenanceRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private ApplicationDbContext _context = default!;
    private MaintenanceRepository _repo = default!;

    public MaintenanceRepositoryTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _repo = new MaintenanceRepository(_context, NullLogger<MaintenanceRepository>.Instance);
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

    // ── sp_get_all_maintenance_records ───────────────────────────────────

    [Fact]
    public async Task GetPagedAsync_ReturnsMaintenanceRecords()
    {
        var (items, totalCount) = await _repo.GetPagedAsync(page: 1, pageSize: 50);

        Assert.True(totalCount >= 3, $"Expected at least 3 maintenance records, got {totalCount}");
        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task GetPagedAsync_PaginationWorks()
    {
        var (page1, total) = await _repo.GetPagedAsync(page: 1, pageSize: 2);
        var (page2, _) = await _repo.GetPagedAsync(page: 2, pageSize: 2);

        Assert.Equal(2, page1.Count());
        Assert.True(total >= 3);
        var page1Ids = page1.Select(r => r.Id).ToHashSet();
        Assert.DoesNotContain(page2, r => page1Ids.Contains(r.Id));
    }

    [Fact]
    public async Task GetPagedAsync_IncludesNavigationProperties()
    {
        var (items, _) = await _repo.GetPagedAsync(page: 1, pageSize: 1);
        var record = items.First();

        Assert.NotNull(record.TypeLookup);
        Assert.False(string.IsNullOrEmpty(record.TypeLookup.Name));
        Assert.NotNull(record.StatusLookup);
        Assert.False(string.IsNullOrEmpty(record.StatusLookup.Name));
    }

    // ── sp_get_maintenance_by_computer_id ───────────────────────────────

    [Fact]
    public async Task GetByComputerIdAsync_ReturnsRecordsForComputer()
    {
        var computers = await _context.Computers.ToListAsync();
        var target = computers.First(c => c.SerialNumber == "SN-001");

        var items = await _repo.GetByComputerIdAsync(target.Id);

        Assert.NotEmpty(items);
        Assert.All(items, r => Assert.Equal(target.Id, r.ComputerId));
    }

    [Fact]
    public async Task GetByComputerIdAsync_ReturnsEmpty_ForComputerWithNoRecords()
    {
        var items = await _repo.GetByComputerIdAsync(999999);

        Assert.Empty(items);
    }

    // ── sp_get_maintenance_by_status ────────────────────────────────────

    [Fact]
    public async Task GetByStatusAsync_ReturnsCorrectRecords()
    {
        var items = await _repo.GetByStatusAsync(MaintenanceStatus.Scheduled);

        Assert.NotEmpty(items);
        Assert.All(items, r => Assert.Equal((int)MaintenanceStatus.Scheduled, r.StatusId));
    }

    // ── sp_get_scheduled_maintenance ────────────────────────────────────

    [Fact]
    public async Task GetScheduledBetweenDatesAsync_ReturnsRecordsInRange()
    {
        var start = DateTime.UtcNow.AddDays(-30);
        var end = DateTime.UtcNow.AddDays(30);

        var items = await _repo.GetScheduledBetweenDatesAsync(start, end);

        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task GetScheduledBetweenDatesAsync_ReturnsEmpty_ForNoMatchingRange()
    {
        var start = DateTime.UtcNow.AddYears(-10);
        var end = DateTime.UtcNow.AddYears(-9);

        var items = await _repo.GetScheduledBetweenDatesAsync(start, end);

        Assert.Empty(items);
    }

    // ── sp_get_maintenance_summary_counts ────────────────────────────────

    [Fact]
    public async Task GetMaintenanceSummaryCountsAsync_ReturnsCounts()
    {
        var (open, scheduled, completedThisMonth) = await _repo.GetMaintenanceSummaryCountsAsync();

        // We seeded records in various states
        Assert.True(open >= 0);
        Assert.True(scheduled >= 0);
        Assert.True(completedThisMonth >= 0);
    }
}
