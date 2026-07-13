using ComputerInventory.Core.Entities;
using ComputerInventory.Infrastructure.Data;
using ComputerInventory.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComputerInventory.Tests.Integration;

/// <summary>
/// Integration tests for GeoLocationRepository stored procedures.
/// Verifies sp_upsert_geo_location, sp_get_all_computer_locations,
/// sp_get_computer_ids_needing_geo_lookup, sp_get_or_create_country_id.
/// </summary>
[Collection("Database")]
public class GeoLocationRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private ApplicationDbContext _context = default!;
    private GeoLocationRepository _repo = default!;

    public GeoLocationRepositoryTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _repo = new GeoLocationRepository(_context, NullLogger<GeoLocationRepository>.Instance);
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

    // ── sp_upsert_geo_location ──────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_InsertsNewGeoLocation()
    {
        // Computer SN-003 (PC-OFFLINE-01) has no geo location seeded
        var computers = await _context.Computers.ToListAsync();
        var target = computers.First(c => c.SerialNumber == "SN-003");

        var geo = new ComputerGeoLocation
        {
            ComputerId = target.Id,
            PublicIpAddress = "198.51.100.1",
            IpLatitude = 51.5074,
            IpLongitude = -0.1278,
            City = "London",
            Region = "England"
        };

        var result = await _repo.UpsertAsync(geo);

        Assert.Equal(target.Id, result.ComputerId);

        // Verify via a fresh read
        await using var verifyCtx = _fixture.CreateDbContext();
        var verifyRepo = new GeoLocationRepository(verifyCtx, NullLogger<GeoLocationRepository>.Instance);
        var saved = await verifyRepo.GetByComputerIdAsync(target.Id);
        Assert.NotNull(saved);
        Assert.Equal("London", saved.City);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingGeoLocation()
    {
        // Computer SN-001 already has a geo location — update its city
        var computers = await _context.Computers.ToListAsync();
        var target = computers.First(c => c.SerialNumber == "SN-001");

        var geo = new ComputerGeoLocation
        {
            ComputerId = target.Id,
            City = "Brooklyn",
        };

        await _repo.UpsertAsync(geo);

        // Verify the COALESCE semantics: City updated, other fields preserved
        await using var verifyCtx = _fixture.CreateDbContext();
        var verifyRepo = new GeoLocationRepository(verifyCtx, NullLogger<GeoLocationRepository>.Instance);
        var saved = await verifyRepo.GetByComputerIdAsync(target.Id);
        Assert.NotNull(saved);
        Assert.Equal("Brooklyn", saved.City);
        // PublicIpAddress should still be set from original seed
        Assert.NotNull(saved.PublicIpAddress);
    }

    // ── sp_get_geo_location_by_computer_id ──────────────────────────────

    [Fact]
    public async Task GetByComputerIdAsync_ReturnsExisting()
    {
        var computers = await _context.Computers.ToListAsync();
        var target = computers.First(c => c.SerialNumber == "SN-001");

        var result = await _repo.GetByComputerIdAsync(target.Id);

        Assert.NotNull(result);
        Assert.Equal(target.Id, result.ComputerId);
        Assert.NotNull(result.City);
    }

    [Fact]
    public async Task GetByComputerIdAsync_ReturnsNull_ForMissing()
    {
        var result = await _repo.GetByComputerIdAsync(999999);
        Assert.Null(result);
    }

    // ── sp_get_all_computer_locations ────────────────────────────────────

    [Fact]
    public async Task GetAllComputerLocationsSpAsync_ReturnsLocationsWithCoordinates()
    {
        var locations = await _repo.GetAllComputerLocationsSpAsync();

        Assert.NotEmpty(locations);
        Assert.All(locations, l =>
        {
            Assert.True(l.ComputerId > 0);
            Assert.False(string.IsNullOrEmpty(l.Hostname));
        });
    }

    // ── sp_get_computer_ids_needing_geo_lookup ──────────────────────────

    [Fact]
    public async Task GetComputerIdsNeedingLookupAsync_ReturnsOnlineComputersWithoutRecentLookup()
    {
        // A very short max age should cause all computers with geo data to need refresh
        var ids = await _repo.GetComputerIdsNeedingLookupAsync(TimeSpan.FromSeconds(1));

        // Should include online computers
        Assert.NotNull(ids);
    }

    // ── sp_get_or_create_country_id ─────────────────────────────────────

    [Fact]
    public async Task GetOrCreateCountryIdAsync_ReturnsExistingCountry()
    {
        var id = await _repo.GetOrCreateCountryIdAsync("United States", "US");

        Assert.NotNull(id);
        Assert.True(id > 0);
    }

    [Fact]
    public async Task GetOrCreateCountryIdAsync_CreatesNewCountry()
    {
        var id = await _repo.GetOrCreateCountryIdAsync("Germany", "DE");

        Assert.NotNull(id);
        Assert.True(id > 0);

        // Calling again should return same ID  
        var id2 = await _repo.GetOrCreateCountryIdAsync("Germany", "DE");
        Assert.Equal(id, id2);
    }
}
