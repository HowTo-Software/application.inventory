using System.Data;
using System.Data.Common;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace ComputerInventory.Infrastructure.Repositories;

/// <summary>
/// Repository for managing computer geolocation data.
/// </summary>
public class GeoLocationRepository : IGeoLocationRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GeoLocationRepository> _logger;

    public GeoLocationRepository(ApplicationDbContext context, ILogger<GeoLocationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public IQueryable<ComputerGeoLocation> Query => _context.ComputerGeoLocations.AsNoTracking();

    public async Task<ComputerGeoLocation?> GetByComputerIdAsync(int computerId, CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_geo_location_by_computer_id] @computer_id";
            command.Parameters.Add(new SqlParameter("@computer_id", SqlDbType.Int) { Value = computerId });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return MapGeoLocationFromReader(reader);
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<IEnumerable<ComputerGeoLocation>> GetAllWithComputersAsync(CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_all_geo_locations_with_computers]";

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var items = new List<ComputerGeoLocation>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var geo = MapGeoLocationFromReader(reader);
                geo.Computer = new Computer
                {
                    Id = geo.ComputerId,
                    Hostname = reader["_computer_hostname"] as string ?? string.Empty,
                    SerialNumber = reader["_computer_serial_number"] as string ?? string.Empty,
                    IpAddress = reader["_computer_ip_address"] as string,
                };
                items.Add(geo);
            }
            return items;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<IEnumerable<ComputerGeoLocation>> GetWithValidCoordinatesAsync(CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_geo_locations_with_valid_coordinates]";

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var items = new List<ComputerGeoLocation>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var geo = MapGeoLocationFromReader(reader);
                geo.Computer = new Computer
                {
                    Id = geo.ComputerId,
                    Hostname = reader["_computer_hostname"] as string ?? string.Empty,
                    SerialNumber = reader["_computer_serial_number"] as string ?? string.Empty,
                    IpAddress = reader["_computer_ip_address"] as string,
                };
                items.Add(geo);
            }
            return items;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<ComputerGeoLocation> UpsertAsync(ComputerGeoLocation geoLocation, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.ExecuteSpNonQueryAsync(
                "sp_upsert_geo_location", cancellationToken,
                SpParam.Input("@computer_id", geoLocation.ComputerId),
                SpParam.Input("@public_ip_address", geoLocation.PublicIpAddress),
                SpParam.Input("@gps_latitude", geoLocation.GpsLatitude),
                SpParam.Input("@gps_longitude", geoLocation.GpsLongitude),
                SpParam.Input("@gps_last_updated", geoLocation.GpsLastUpdated),
                SpParam.Input("@gps_accuracy", geoLocation.GpsAccuracyMeters),
                SpParam.Input("@ip_latitude", geoLocation.IpLatitude),
                SpParam.Input("@ip_longitude", geoLocation.IpLongitude),
                SpParam.Input("@ip_last_updated", geoLocation.IpLastUpdated),
                SpParam.Input("@latitude", geoLocation.Latitude),
                SpParam.Input("@longitude", geoLocation.Longitude),
                SpParam.Input("@city", geoLocation.City),
                SpParam.Input("@region", geoLocation.Region),
                SpParam.Input("@country_id", geoLocation.CountryId),
                SpParam.Input("@isp", geoLocation.Isp),
                SpParam.Input("@timezone", geoLocation.Timezone),
                SpParam.Input("@lookup_successful", geoLocation.LookupSuccessful),
                SpParam.Input("@lookup_error", geoLocation.LookupError),
                SpParam.Input("@location_source", geoLocation.LocationSource));

            // Re-fetch the upserted row so the caller gets the merged result
            return await GetByComputerIdAsync(geoLocation.ComputerId, cancellationToken)
                ?? throw new InvalidOperationException($"Upserted geo location for computer {geoLocation.ComputerId} not found after upsert");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert geolocation for computer {ComputerId}", geoLocation.ComputerId);
            throw;
        }
    }

    public async Task UpdateGpsLocationAsync(int computerId, double latitude, double longitude, string? publicIpAddress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.ExecuteSpNonQueryAsync(
                "sp_update_gps_location", cancellationToken,
                SpParam.Input("@computer_id", computerId),
                SpParam.Input("@latitude", latitude),
                SpParam.Input("@longitude", longitude),
                SpParam.Input("@public_ip_address", (object?)publicIpAddress ?? DBNull.Value));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update GPS location for computer {ComputerId}", computerId);
            throw;
        }
    }

    public async Task TrackGpsLocationUpdateAsync(int computerId, double latitude, double longitude, string? publicIpAddress = null, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ExecuteSpSingleTrackedAsync<ComputerGeoLocation>(
            "sp_get_geo_location_simple", cancellationToken,
            SpParam.Input("@computer_id", computerId));

        if (existing == null)
        {
            existing = new ComputerGeoLocation
            {
                ComputerId = computerId,
                PublicIpAddress = publicIpAddress
            };
            _context.ComputerGeoLocations.Add(existing);
        }

        existing.GpsLatitude = latitude;
        existing.GpsLongitude = longitude;
        existing.GpsLastUpdated = DateTime.UtcNow;
        existing.Latitude = latitude;
        existing.Longitude = longitude;
        existing.LocationSource = "GPS";
        existing.LastUpdated = DateTime.UtcNow;
        existing.LookupSuccessful = true;
        existing.LookupError = null;

        if (!string.IsNullOrEmpty(publicIpAddress))
        {
            existing.PublicIpAddress = publicIpAddress;
        }
    }

    public async Task UpdateIpLocationAsync(int computerId, double latitude, double longitude, string? city, string? region, string? country, string? countryCode, string? isp, string? timezone, string? publicIpAddress, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.ExecuteSpNonQueryAsync(
                "sp_update_ip_location", cancellationToken,
                SpParam.Input("@computer_id", computerId),
                SpParam.Input("@latitude", latitude),
                SpParam.Input("@longitude", longitude),
                SpParam.Input("@city", (object?)city ?? DBNull.Value),
                SpParam.Input("@region", (object?)region ?? DBNull.Value),
                SpParam.Input("@country", (object?)country ?? DBNull.Value),
                SpParam.Input("@country_code", (object?)countryCode ?? DBNull.Value),
                SpParam.Input("@isp", (object?)isp ?? DBNull.Value),
                SpParam.Input("@timezone", (object?)timezone ?? DBNull.Value),
                SpParam.Input("@public_ip_address", (object?)publicIpAddress ?? DBNull.Value));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update IP location for computer {ComputerId}", computerId);
            throw;
        }
    }

    public async Task DeleteAsync(int computerId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.ExecuteSpNonQueryAsync(
                "sp_delete_geo_location", cancellationToken,
                SpParam.Input("@computer_id", computerId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete geolocation for computer {ComputerId}", computerId);
            throw;
        }
    }

    public async Task<IEnumerable<int>> GetComputerIdsNeedingLookupAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        var cutoffHours = (int)Math.Ceiling(maxAge.TotalHours);

        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_computer_ids_needing_geo_lookup] @cutoff_hours";
            command.Parameters.Add(new SqlParameter("@cutoff_hours", SqlDbType.Int) { Value = cutoffHours });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var ids = new List<int>();
            while (await reader.ReadAsync(cancellationToken))
                ids.Add(reader.GetInt32(0));

            return ids;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    // ── SP result-set mapper ────────────────────────────────────────────

    private static ComputerGeoLocation MapGeoLocationFromReader(DbDataReader r)
    {
        var geo = new ComputerGeoLocation
        {
            Id = (int)r["id"],
            ComputerId = (int)r["computer_id"],
            PublicIpAddress = r["public_ip_address"] as string,
            GpsLatitude = r["gps_latitude"] as double?,
            GpsLongitude = r["gps_longitude"] as double?,
            GpsLastUpdated = r["gps_last_updated"] as DateTime?,
            IpLatitude = r["ip_latitude"] as double?,
            IpLongitude = r["ip_longitude"] as double?,
            IpLastUpdated = r["ip_last_updated"] as DateTime?,
            Latitude = r["latitude"] as double?,
            Longitude = r["longitude"] as double?,
            City = r["city"] as string,
            Region = r["region"] as string,
            CountryId = r["country_id"] as int?,
            Isp = r["isp"] as string,
            Timezone = r["timezone"] as string,
            LastUpdated = r["last_updated"] is DBNull ? DateTime.MinValue : (DateTime)r["last_updated"],
            LookupSuccessful = r["lookup_successful"] as bool? ?? false,
            LookupError = r["lookup_error"] as string,
            LocationSource = r["location_source"] as string ?? "IP",
            GpsAccuracyMeters = r["gps_accuracy_meters"] as double?,
        };

        if (!r.IsDBNull(r.GetOrdinal("_country_id")))
        {
            geo.CountryEntity = new Country
            {
                Id = (int)r["_country_id"],
                CountryCode = r["_country_code"] as string ?? string.Empty,
                Name = r["_country_name"] as string ?? string.Empty,
            };
        }

        return geo;
    }

    public async Task<List<ComputerLocationDto>> GetAllComputerLocationsSpAsync(CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_all_computer_locations]";

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var results = new List<ComputerLocationDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var dto = MapLocationDtoFromReader(reader);

                // Legacy fallback columns not on the DTO — read once for coordinate resolution
                var legacyLat = reader["legacy_latitude"] as double?;
                var legacyLon = reader["legacy_longitude"] as double?;
                var legacySource = reader["legacy_location_source"] as string;
                ResolveActiveCoordinates(dto, legacyLat, legacyLon, legacySource);

                results.Add(dto);
            }

            return results;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<DateTime?> GetLastSuccessfulLookupTimeAsync(CancellationToken cancellationToken = default)
    {
        var lastUpdate = await _context.ComputerGeoLocations
            .Where(g => g.LookupSuccessful)
            .OrderByDescending(g => g.LastUpdated)
            .Select(g => g.LastUpdated)
            .FirstOrDefaultAsync(cancellationToken);

        return lastUpdate == default ? null : lastUpdate;
    }

    public async Task<int?> GetOrCreateCountryIdAsync(string? country, string? countryCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(country)) return null;

        return await _context.ExecuteSpScalarAsync<int?>(
            "sp_get_or_create_country_id", cancellationToken,
            SpParam.Input("@country_code", countryCode ?? ""),
            SpParam.Input("@country_name", country));
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static ComputerLocationDto MapLocationDtoFromReader(DbDataReader r)
    {
        return new ComputerLocationDto
        {
            ComputerId = (int)r["id"],
            Hostname = r["hostname"] as string ?? string.Empty,
            SerialNumber = r["serial_number"] as string ?? string.Empty,
            PublicIpAddress = r["public_ip_address"] as string,
            IsOnline = r["is_online"] is true,
            LastSeen = r["last_seen"] as DateTime?,
            CurrentUser = r["current_user"] as string,
            Status = (r["status_id"] as int?)?.ToString() ?? string.Empty,
            PreferredLocationSource = r["preferred_location_source"] as string ?? "Auto",
            GpsLatitude = r["gps_latitude"] as double?,
            GpsLongitude = r["gps_longitude"] as double?,
            GpsLastUpdated = r["gps_last_updated"] as DateTime?,
            IpLatitude = r["ip_latitude"] as double?,
            IpLongitude = r["ip_longitude"] as double?,
            IpLastUpdated = r["ip_last_updated"] as DateTime?,
            City = r["city"] as string,
            Region = r["region"] as string,
            Country = r["country"] as string,
            CountryCode = r["country_code"] as string,
            LocationUpdated = r["location_updated"] as DateTime?
        };
    }

    private static void ResolveActiveCoordinates(
        ComputerLocationDto dto, double? legacyLat, double? legacyLon, string? legacySource)
    {
        var pref = dto.PreferredLocationSource ?? "Auto";
        if (pref == "GPS" && dto.HasGpsLocation)
        {
            dto.Latitude = dto.GpsLatitude;
            dto.Longitude = dto.GpsLongitude;
            dto.LocationSource = "GPS";
        }
        else if (pref == "IP" && dto.HasIpLocation)
        {
            dto.Latitude = dto.IpLatitude;
            dto.Longitude = dto.IpLongitude;
            dto.LocationSource = "IP";
        }
        else // Auto: prefer GPS, fallback to IP
        {
            if (dto.HasGpsLocation)
            {
                dto.Latitude = dto.GpsLatitude;
                dto.Longitude = dto.GpsLongitude;
                dto.LocationSource = "GPS";
            }
            else if (dto.HasIpLocation)
            {
                dto.Latitude = dto.IpLatitude;
                dto.Longitude = dto.IpLongitude;
                dto.LocationSource = "IP";
            }
            else
            {
                // Fallback to legacy Latitude/Longitude if new columns aren't populated yet
                dto.Latitude = legacyLat;
                dto.Longitude = legacyLon;
                dto.LocationSource = legacySource ?? "IP";
            }
        }
    }
}
