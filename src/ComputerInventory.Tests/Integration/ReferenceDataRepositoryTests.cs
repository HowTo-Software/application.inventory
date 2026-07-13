using ComputerInventory.Infrastructure.Data;
using ComputerInventory.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComputerInventory.Tests.Integration;

/// <summary>
/// Integration tests for ReferenceDataRepository stored procedures.
/// Verifies sp_find_or_create_manufacturer, sp_find_or_create_computer_model,
/// sp_find_or_create_operating_system, sp_find_or_create_department, sp_find_or_create_location.
/// </summary>
[Collection("Database")]
public class ReferenceDataRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private ApplicationDbContext _context = default!;
    private ReferenceDataRepository _repo = default!;

    public ReferenceDataRepositoryTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _repo = new ReferenceDataRepository(_context, NullLogger<ReferenceDataRepository>.Instance);
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

    // ── sp_find_or_create_manufacturer ───────────────────────────────────

    [Fact]
    public async Task FindOrCreateManufacturerAsync_FindsExisting()
    {
        var id = await _repo.FindOrCreateManufacturerAsync("Dell");

        Assert.NotNull(id);
        Assert.True(id > 0);
    }

    [Fact]
    public async Task FindOrCreateManufacturerAsync_CreatesNew()
    {
        var id = await _repo.FindOrCreateManufacturerAsync("Lenovo");

        Assert.NotNull(id);
        Assert.True(id > 0);

        // Calling again returns same ID
        var id2 = await _repo.FindOrCreateManufacturerAsync("Lenovo");
        Assert.Equal(id, id2);
    }

    // ── sp_find_or_create_computer_model ────────────────────────────────

    [Fact]
    public async Task FindOrCreateComputerModelAsync_FindsExisting()
    {
        var mfgId = await _repo.FindOrCreateManufacturerAsync("Dell");
        var id = await _repo.FindOrCreateComputerModelAsync(mfgId!.Value, "Latitude 5520");

        Assert.NotNull(id);
        Assert.True(id > 0);
    }

    [Fact]
    public async Task FindOrCreateComputerModelAsync_CreatesNew()
    {
        var mfgId = await _repo.FindOrCreateManufacturerAsync("Dell");
        var id = await _repo.FindOrCreateComputerModelAsync(mfgId!.Value, "OptiPlex 7090");

        Assert.NotNull(id);
        Assert.True(id > 0);

        // Idempotent
        var id2 = await _repo.FindOrCreateComputerModelAsync(mfgId.Value, "OptiPlex 7090");
        Assert.Equal(id, id2);
    }

    // ── sp_find_or_create_operating_system ──────────────────────────────

    [Fact]
    public async Task FindOrCreateOperatingSystemAsync_FindsExisting()
    {
        var id = await _repo.FindOrCreateOperatingSystemAsync("Windows 11 Pro");

        Assert.NotNull(id);
        Assert.True(id > 0);
    }

    [Fact]
    public async Task FindOrCreateOperatingSystemAsync_CreatesNew()
    {
        var id = await _repo.FindOrCreateOperatingSystemAsync("Ubuntu 24.04 LTS");

        Assert.NotNull(id);
        Assert.True(id > 0);

        var id2 = await _repo.FindOrCreateOperatingSystemAsync("Ubuntu 24.04 LTS");
        Assert.Equal(id, id2);
    }

    // ── sp_find_or_create_department ────────────────────────────────────

    [Fact]
    public async Task FindOrCreateDepartmentAsync_FindsExisting()
    {
        var id = await _repo.FindOrCreateDepartmentAsync("IT");

        Assert.NotNull(id);
        Assert.True(id > 0);
    }

    [Fact]
    public async Task FindOrCreateDepartmentAsync_CreatesNew()
    {
        var id = await _repo.FindOrCreateDepartmentAsync("Marketing");

        Assert.NotNull(id);
        Assert.True(id > 0);

        var id2 = await _repo.FindOrCreateDepartmentAsync("Marketing");
        Assert.Equal(id, id2);
    }

    // ── sp_find_or_create_location ──────────────────────────────────────

    [Fact]
    public async Task FindOrCreateLocationAsync_FindsExisting()
    {
        var id = await _repo.FindOrCreateLocationAsync("Building A");

        Assert.NotNull(id);
        Assert.True(id > 0);
    }

    [Fact]
    public async Task FindOrCreateLocationAsync_CreatesNew()
    {
        var id = await _repo.FindOrCreateLocationAsync("Building Z");

        Assert.NotNull(id);
        Assert.True(id > 0);

        var id2 = await _repo.FindOrCreateLocationAsync("Building Z");
        Assert.Equal(id, id2);
    }
}
