using ComputerInventory.Core.Entities;
using ComputerInventory.Infrastructure.Data;
using ComputerInventory.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComputerInventory.Tests.Integration;

/// <summary>
/// Integration tests for ComputerRepository stored procedures.
/// Verifies sp_get_all_computers, sp_get_computer_by_id, sp_get_computer_by_serial_number,
/// sp_search_computers, sp_get_computers_by_status/location/department, sp_get_total_computer_count,
/// sp_get_status_counts, sp_delete_computer_direct.
/// </summary>
[Collection("Database")]
public class ComputerRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private ApplicationDbContext _context = default!;
    private ComputerRepository _repo = default!;

    public ComputerRepositoryTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _repo = new ComputerRepository(_context, NullLogger<ComputerRepository>.Instance);
        await TestDataSeeder.SeedAsync(_fixture.CreateDbContext());
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        // Clean up seeded data for isolation
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

    // ── sp_get_all_computers ────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsAllNonDeletedComputers()
    {
        var (items, totalCount) = await _repo.GetAllAsync(page: 1, pageSize: 50);

        Assert.True(totalCount >= 5, $"Expected at least 5 computers, got {totalCount}");
        Assert.Equal(totalCount, items.Count());
    }

    [Fact]
    public async Task GetAllAsync_PaginationWorks()
    {
        var (page1, total) = await _repo.GetAllAsync(page: 1, pageSize: 2);
        var (page2, _) = await _repo.GetAllAsync(page: 2, pageSize: 2);

        Assert.Equal(2, page1.Count());
        Assert.True(total >= 5);
        // Pages should not overlap
        var page1Ids = page1.Select(c => c.Id).ToHashSet();
        Assert.DoesNotContain(page2, c => page1Ids.Contains(c.Id));
    }

    [Fact]
    public async Task GetAllAsync_IncludesNavigationProperties()
    {
        var (items, _) = await _repo.GetAllAsync(page: 1, pageSize: 1);
        var computer = items.First();

        Assert.NotNull(computer.ComputerModel);
        Assert.False(string.IsNullOrEmpty(computer.ComputerModel.ModelName));
        Assert.NotNull(computer.ComputerModel.Manufacturer);
        Assert.False(string.IsNullOrEmpty(computer.ComputerModel.Manufacturer.Name));
        Assert.NotNull(computer.StatusLookup);
        Assert.NotNull(computer.TypeLookup);
        Assert.NotNull(computer.AgentStatus);
    }

    [Fact]
    public async Task GetAllAsync_FilterByStatus()
    {
        var (items, totalCount) = await _repo.GetAllAsync(page: 1, pageSize: 50, status: "Unassigned");

        Assert.True(totalCount >= 1);
        Assert.All(items, c => Assert.Equal("Unassigned", c.StatusLookup.Name));
    }

    [Fact]
    public async Task GetAllAsync_SortByHostname()
    {
        var (items, _) = await _repo.GetAllAsync(page: 1, pageSize: 50, sortBy: "hostname", sortDirection: "ASC");
        var hostnames = items.Select(c => c.Hostname).ToList();

        Assert.Equal(hostnames.OrderBy(h => h), hostnames);
    }

    // ── sp_get_computer_by_id ───────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ReturnsComputer_WithMaintenanceAndHistory()
    {
        // Get the first computer (SN-001) which has maintenance records and history
        var (items, _) = await _repo.GetAllAsync(page: 1, pageSize: 50);
        var target = items.First(c => c.SerialNumber == "SN-001");

        // Use a fresh context for the by-id query
        await using var ctx = _fixture.CreateDbContext();
        var repo = new ComputerRepository(ctx, NullLogger<ComputerRepository>.Instance);

        var computer = await repo.GetByIdAsync(target.Id);

        Assert.NotNull(computer);
        Assert.Equal("SN-001", computer.SerialNumber);
        Assert.NotNull(computer.ComputerModel);
        Assert.NotNull(computer.StatusLookup);
        Assert.NotNull(computer.AgentStatus);
        Assert.NotEmpty(computer.MaintenanceRecords);
        Assert.NotEmpty(computer.History);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_ForNonExistentId()
    {
        var result = await _repo.GetByIdAsync(999999);
        Assert.Null(result);
    }

    // ── sp_get_computer_by_serial_number ────────────────────────────────

    [Fact]
    public async Task GetBySerialNumberAsync_FindsExisting()
    {
        var computer = await _repo.GetBySerialNumberAsync("SN-001");

        Assert.NotNull(computer);
        Assert.Equal("SN-001", computer.SerialNumber);
        Assert.NotNull(computer.AgentStatus);
    }

    [Fact]
    public async Task GetBySerialNumberAsync_ReturnsNull_ForMissing()
    {
        var result = await _repo.GetBySerialNumberAsync("NONEXISTENT-SN");
        Assert.Null(result);
    }

    // ── sp_search_computers ─────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_FindsByHostname()
    {
        var results = await _repo.SearchAsync("SEARCH-TEST");

        Assert.Single(results);
        Assert.Equal("PC-SEARCH-TEST", results.First().Hostname);
    }

    [Fact]
    public async Task SearchAsync_FindsBySerialNumber()
    {
        var results = await _repo.SearchAsync("SN-003");

        Assert.Single(results);
        Assert.Equal("SN-003", results.First().SerialNumber);
    }

    [Fact]
    public async Task SearchAsync_FindsByIpAddress()
    {
        var results = await _repo.SearchAsync("10.0.0.100");

        Assert.Contains(results, c => c.SerialNumber == "SN-005");
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_ForNoMatch()
    {
        var results = await _repo.SearchAsync("ZZZZZ-NO-MATCH");

        Assert.Empty(results);
    }

    // ── sp_get_computers_by_status ──────────────────────────────────────

    [Fact]
    public async Task GetByStatusAsync_ReturnsCorrectComputers()
    {
        var items = await _repo.GetByStatusAsync(ComputerStatus.Retired);

        Assert.All(items, c => Assert.Equal((int)ComputerStatus.Retired, c.StatusId));
    }

    [Fact]
    public async Task GetByStatusAsync_Paged()
    {
        var (items, total) = await _repo.GetByStatusAsync(ComputerStatus.Available, page: 1, pageSize: 1);

        Assert.Single(items);
        Assert.True(total >= 1);
    }

    // ── sp_get_computers_by_location ────────────────────────────────────

    [Fact]
    public async Task GetByLocationAsync_ReturnsCorrectComputers()
    {
        var (allItems, _) = await _repo.GetAllAsync(page: 1, pageSize: 1);
        var locationId = allItems.First().LocationId!.Value;

        var items = await _repo.GetByLocationAsync(locationId);

        Assert.NotEmpty(items);
        Assert.All(items, c => Assert.Equal(locationId, c.LocationId));
    }

    // ── sp_get_computers_by_department ──────────────────────────────────

    [Fact]
    public async Task GetByDepartmentAsync_ReturnsCorrectComputers()
    {
        var (allItems, _) = await _repo.GetAllAsync(page: 1, pageSize: 1);
        var deptId = allItems.First().DepartmentId!.Value;

        var items = await _repo.GetByDepartmentAsync(deptId);

        Assert.NotEmpty(items);
        Assert.All(items, c => Assert.Equal(deptId, c.DepartmentId));
    }

    // ── sp_get_total_computer_count ─────────────────────────────────────

    [Fact]
    public async Task GetTotalCountAsync_ReturnsCorrectCount()
    {
        var count = await _repo.GetTotalCountAsync();

        Assert.True(count >= 5, $"Expected at least 5 computers, got {count}");
    }

    // ── sp_get_status_counts ────────────────────────────────────────────

    [Fact]
    public async Task GetStatusCountsAsync_ReturnsGroupedCounts()
    {
        var counts = await _repo.GetStatusCountsAsync();

        Assert.NotEmpty(counts);
        Assert.Contains(ComputerStatus.Available, counts.Keys);
        Assert.True(counts.Values.All(v => v > 0));
    }

    // ── sp_delete_computer_direct ───────────────────────────────────────

    [Fact]
    public async Task DeleteDirectAsync_SoftDeletesComputer()
    {
        // Create a computer to delete
        await using var seedCtx = _fixture.CreateDbContext();
        var manufacturer = await seedCtx.Manufacturers.FirstAsync();
        var model = await seedCtx.ComputerModels.FirstAsync();

        var computer = new Computer
        {
            SerialNumber = "SN-DELETE-TEST",
            Hostname = "PC-DELETE-TEST",
            ModelId = model.Id,
            TypeId = 1,
            StatusId = 1,
            PurchaseDate = DateTime.UtcNow,
        };
        seedCtx.Computers.Add(computer);
        await seedCtx.SaveChangesAsync();
        var id = computer.Id;

        // Perform soft delete
        var deleted = await _repo.DeleteDirectAsync(id);
        Assert.True(deleted);

        // Verify it's soft-deleted (no longer returned by SP which filters is_deleted = 0)
        await using var verifyCtx = _fixture.CreateDbContext();
        var verifyRepo = new ComputerRepository(verifyCtx, NullLogger<ComputerRepository>.Instance);
        var result = await verifyRepo.GetByIdAsync(id);
        Assert.Null(result);
    }

    // ── sp_get_dashboard_counts ─────────────────────────────────────────

    [Fact]
    public async Task GetDashboardCountsAsync_ReturnsPopulatedCounts()
    {
        var counts = await _repo.GetDashboardCountsAsync("1.0.0");

        Assert.True(counts.TotalComputers >= 5);
        Assert.True(counts.OnlineComputers >= 1);
        Assert.True(counts.AgentsInstalled >= 1);
    }
}
