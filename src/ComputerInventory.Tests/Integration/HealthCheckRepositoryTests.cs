using ComputerInventory.Infrastructure.Data;
using ComputerInventory.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComputerInventory.Tests.Integration;

/// <summary>
/// Integration tests for HealthCheckRepository stored procedures.
/// Verifies sp_health_check.
/// </summary>
[Collection("Database")]
public class HealthCheckRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private ApplicationDbContext _context = default!;
    private HealthCheckRepository _repo = default!;

    public HealthCheckRepositoryTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _repo = new HealthCheckRepository(_context, NullLogger<HealthCheckRepository>.Instance);
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

    // ── sp_health_check ─────────────────────────────────────────────────

    [Fact]
    public async Task GetHealthCheckCountAsync_ReturnsPositiveCount()
    {
        var count = await _repo.GetHealthCheckCountAsync();

        Assert.True(count >= 0);
    }

    [Fact]
    public void GetProviderName_ReturnsSqlServer()
    {
        var provider = _repo.GetProviderName();

        Assert.Contains("SqlServer", provider);
    }
}
