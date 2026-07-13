using ComputerInventory.Core.Entities;

namespace ComputerInventory.Core.Interfaces;

/// <summary>
/// Repository interface for managing computer geolocation data.
/// </summary>
public interface IGeoLocationRepository
{
    /// <summary>
    /// Deferred IQueryable for composing custom queries without materializing.
    /// Returned as no-tracking by default.
    /// </summary>
    IQueryable<ComputerGeoLocation> Query { get; }

    /// <summary>
    /// Get geolocation data for a specific computer.
    /// </summary>
    Task<ComputerGeoLocation?> GetByComputerIdAsync(int computerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all geolocations with their associated computers.
    /// </summary>
    Task<IEnumerable<ComputerGeoLocation>> GetAllWithComputersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get geolocations that have valid coordinates (for map display).
    /// </summary>
    Task<IEnumerable<ComputerGeoLocation>> GetWithValidCoordinatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Add or update geolocation for a computer.
    /// </summary>
    Task<ComputerGeoLocation> UpsertAsync(ComputerGeoLocation geoLocation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update GPS coordinates for a computer.
    /// </summary>
    Task UpdateGpsLocationAsync(int computerId, double latitude, double longitude, string? publicIpAddress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Track GPS location changes without calling SaveChanges.
    /// Use with a subsequent SaveChangesAsync call to batch multiple changes.
    /// </summary>
    Task TrackGpsLocationUpdateAsync(int computerId, double latitude, double longitude, string? publicIpAddress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update IP-based location for a computer.
    /// </summary>
    Task UpdateIpLocationAsync(int computerId, double latitude, double longitude, string? city, string? region, string? country, string? countryCode, string? isp, string? timezone, string? publicIpAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete geolocation data for a computer.
    /// </summary>
    Task DeleteAsync(int computerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get geolocations that need IP lookup (no valid coordinates or stale data).
    /// </summary>
    Task<IEnumerable<int>> GetComputerIdsNeedingLookupAsync(TimeSpan maxAge, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all computer locations via sp_get_all_computer_locations stored procedure.
    /// Returns DTOs with resolved coordinates based on preferred location source.
    /// </summary>
    Task<List<ComputerLocationDto>> GetAllComputerLocationsSpAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the most recent successful geolocation lookup time.
    /// </summary>
    Task<DateTime?> GetLastSuccessfulLookupTimeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get or create a country by name and code. Returns the country ID.
    /// </summary>
    Task<int?> GetOrCreateCountryIdAsync(string? country, string? countryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save any pending tracked changes.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
