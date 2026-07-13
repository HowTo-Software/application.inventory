using ComputerInventory.Infrastructure.Data;
using ComputerInventory.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComputerInventory.Tests.Integration;

/// <summary>
/// Integration tests for LookupRepository stored procedures.
/// Verifies sp_get_computer_types, sp_get_computer_statuses, sp_get_departments,
/// sp_get_locations, sp_get_manufacturers, sp_get_computer_models, sp_get_operating_systems,
/// sp_get_maintenance_types, sp_get_maintenance_statuses, sp_get_user_roles, sp_get_version_types.
/// </summary>
[Collection("Database")]
public class LookupRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private ApplicationDbContext _context = default!;
    private LookupRepository _repo = default!;

    public LookupRepositoryTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _repo = new LookupRepository(_context, NullLogger<LookupRepository>.Instance);
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

    // ── sp_get_computer_types ───────────────────────────────────────────

    [Fact]
    public async Task GetComputerTypesAsync_ReturnsSeededTypes()
    {
        var types = await _repo.GetComputerTypesAsync();

        Assert.NotEmpty(types);
        Assert.All(types, t =>
        {
            Assert.True(t.Id >= 0);
            Assert.False(string.IsNullOrEmpty(t.Name));
        });
    }

    // ── sp_get_computer_statuses ────────────────────────────────────────

    [Fact]
    public async Task GetComputerStatusesAsync_ReturnsSeededStatuses()
    {
        var statuses = await _repo.GetComputerStatusesAsync();

        Assert.NotEmpty(statuses);
        Assert.Contains(statuses, s => s.Name == "Unassigned");
        Assert.Contains(statuses, s => s.Name == "Assigned");
    }

    // ── sp_get_departments ──────────────────────────────────────────────

    [Fact]
    public async Task GetDepartmentsAsync_ReturnsAllDepartments()
    {
        var departments = await _repo.GetDepartmentsAsync();

        Assert.NotEmpty(departments);
        Assert.Contains(departments, d => d.Name == "IT");
        Assert.Contains(departments, d => d.Name == "Engineering");
    }

    // ── sp_get_locations ────────────────────────────────────────────────

    [Fact]
    public async Task GetLocationsAsync_ReturnsAllLocations()
    {
        var locations = await _repo.GetLocationsAsync();

        Assert.NotEmpty(locations);
        Assert.Contains(locations, l => l.Name == "Building A");
        Assert.Contains(locations, l => l.Name == "Building B");
    }

    // ── sp_get_manufacturers ────────────────────────────────────────────

    [Fact]
    public async Task GetManufacturersAsync_ReturnsManufacturers()
    {
        var manufacturers = await _repo.GetManufacturersAsync();

        Assert.NotEmpty(manufacturers);
        Assert.Contains(manufacturers, m => m.Name == "Dell");
    }

    // ── sp_get_computer_models ──────────────────────────────────────────

    [Fact]
    public async Task GetModelsAsync_ReturnsModelsWithManufacturer()
    {
        var models = await _repo.GetModelsAsync();

        Assert.NotEmpty(models);
        var model = models.First();
        Assert.False(string.IsNullOrEmpty(model.Name));
        Assert.True(model.ManufacturerId > 0);
    }

    [Fact]
    public async Task GetModelsAsync_FiltersByManufacturer()
    {
        var allModels = await _repo.GetModelsAsync();
        var mfgId = allModels.First().ManufacturerId;

        var filtered = await _repo.GetModelsAsync(mfgId);

        Assert.NotEmpty(filtered);
        Assert.All(filtered, m => Assert.Equal(mfgId, m.ManufacturerId));
    }

    // ── sp_get_operating_systems ────────────────────────────────────────

    [Fact]
    public async Task GetOperatingSystemsAsync_ReturnsOperatingSystems()
    {
        var operatingSystems = await _repo.GetOperatingSystemsAsync();

        Assert.NotEmpty(operatingSystems);
        Assert.Contains(operatingSystems, os => os.Name == "Windows 11 Pro");
    }

    // ── sp_get_maintenance_types ────────────────────────────────────────

    [Fact]
    public async Task GetMaintenanceTypesAsync_ReturnsSeededTypes()
    {
        var types = await _repo.GetMaintenanceTypesAsync();

        Assert.NotEmpty(types);
        Assert.All(types, t => Assert.False(string.IsNullOrEmpty(t.Name)));
    }

    // ── sp_get_maintenance_statuses ─────────────────────────────────────

    [Fact]
    public async Task GetMaintenanceStatusesAsync_ReturnsSeededStatuses()
    {
        var statuses = await _repo.GetMaintenanceStatusesAsync();

        Assert.NotEmpty(statuses);
        Assert.Contains(statuses, s => s.Name == "Scheduled");
        Assert.Contains(statuses, s => s.Name == "Completed");
    }

    // ── sp_get_user_roles ───────────────────────────────────────────────

    [Fact]
    public async Task GetUserRolesAsync_ReturnsSeededRoles()
    {
        var roles = await _repo.GetUserRolesAsync();

        Assert.NotEmpty(roles);
        Assert.Contains(roles, r => r.Name == "Admin");
    }

    // ── sp_get_version_types ────────────────────────────────────────────

    [Fact]
    public async Task GetVersionTypesAsync_ReturnsSeededTypes()
    {
        var types = await _repo.GetVersionTypesAsync();

        Assert.NotEmpty(types);
        Assert.All(types, t => Assert.False(string.IsNullOrEmpty(t.Name)));
    }
}
