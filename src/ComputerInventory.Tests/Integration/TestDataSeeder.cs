using ComputerInventory.Core.Entities;
using ComputerInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ComputerInventory.Tests.Integration;

/// <summary>
/// Seeds the test database with reference/lookup data and sample entities
/// that stored procedures depend on.
/// </summary>
public static class TestDataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Clean up any existing seeded data via raw SQL (reverse dependency order)
        await context.Database.ExecuteSqlRawAsync(@"
            DELETE FROM [dbo].[computer_histories];
            DELETE FROM [dbo].[maintenance_records];
            DELETE FROM [dbo].[computer_geo_locations];
            DELETE FROM [dbo].[computer_agent_statuses];
            DELETE FROM [dbo].[computers];
            DELETE FROM [dbo].[version_histories];
            DELETE FROM [dbo].[users];
            DELETE FROM [dbo].[countries];
            DELETE FROM [dbo].[locations];
            DELETE FROM [dbo].[departments];
            DELETE FROM [dbo].[operating_systems];
            DELETE FROM [dbo].[computer_models];
            DELETE FROM [dbo].[manufacturers];
        ");

        // --- Reference data ---
        var manufacturer = new Manufacturer { Name = "Dell" };
        context.Manufacturers.Add(manufacturer);
        await context.SaveChangesAsync();

        var model = new ComputerModel { ManufacturerId = manufacturer.Id, ModelName = "Latitude 5520" };
        context.ComputerModels.Add(model);
        await context.SaveChangesAsync();

        var os = new OperatingSystemEntity { Name = "Windows 11 Pro" };
        context.OperatingSystems.Add(os);
        await context.SaveChangesAsync();

        var dept = new Department { Name = "IT" };
        var dept2 = new Department { Name = "Engineering" };
        context.Departments.AddRange(dept, dept2);
        await context.SaveChangesAsync();

        var location = new Location { Name = "Building A" };
        var location2 = new Location { Name = "Building B" };
        context.Locations.AddRange(location, location2);
        await context.SaveChangesAsync();

        var country = new Country { Name = "United States", CountryCode = "US" };
        context.Countries.Add(country);
        await context.SaveChangesAsync();

        // --- Computers ---
        var computers = new[]
        {
            CreateComputer("SN-001", "PC-ONLINE-01", model.Id, os.Id, 1, (int)ComputerStatus.Available, location.Id, dept.Id),
            CreateComputer("SN-002", "PC-ONLINE-02", model.Id, os.Id, 1, (int)ComputerStatus.Assigned, location.Id, dept.Id),
            CreateComputer("SN-003", "PC-OFFLINE-01", model.Id, os.Id, 1, (int)ComputerStatus.InMaintenance, location2.Id, dept2.Id),
            CreateComputer("SN-004", "PC-RETIRED-01", model.Id, os.Id, 1, (int)ComputerStatus.Retired, location2.Id, dept2.Id),
            CreateComputer("SN-005", "PC-SEARCH-TEST", model.Id, os.Id, 1, (int)ComputerStatus.Available, location.Id, dept.Id, ipAddress: "10.0.0.100"),
        };
        context.Computers.AddRange(computers);
        await context.SaveChangesAsync();

        // --- Agent statuses ---
        var now = DateTime.UtcNow;
        var agentStatuses = new[]
        {
            new ComputerAgentStatus { ComputerId = computers[0].Id, IsOnline = true, HasAgent = true, LastSeen = now.AddMinutes(-2), AgentVersion = "1.0.0", PublicIpAddress = "203.0.113.1" },
            new ComputerAgentStatus { ComputerId = computers[1].Id, IsOnline = true, HasAgent = true, LastSeen = now.AddMinutes(-3), AgentVersion = "1.0.0", PublicIpAddress = "203.0.113.2" },
            new ComputerAgentStatus { ComputerId = computers[2].Id, IsOnline = false, HasAgent = true, LastSeen = now.AddHours(-6), AgentVersion = "0.9.0", PublicIpAddress = "203.0.113.3" },
            new ComputerAgentStatus { ComputerId = computers[3].Id, IsOnline = false, HasAgent = false, LastSeen = now.AddDays(-30) },
            new ComputerAgentStatus { ComputerId = computers[4].Id, IsOnline = true, HasAgent = true, LastSeen = now.AddMinutes(-1), AgentVersion = "1.0.0", PublicIpAddress = "10.0.0.100" },
        };
        context.ComputerAgentStatuses.AddRange(agentStatuses);
        await context.SaveChangesAsync();

        // --- Geo locations ---
        var geoLocations = new[]
        {
            new ComputerGeoLocation { ComputerId = computers[0].Id, PublicIpAddress = "203.0.113.1", GpsLatitude = 40.7128, GpsLongitude = -74.0060, IpLatitude = 40.71, IpLongitude = -74.01, Latitude = 40.7128, Longitude = -74.0060, City = "New York", Region = "NY", CountryId = country.Id },
            new ComputerGeoLocation { ComputerId = computers[1].Id, PublicIpAddress = "203.0.113.2", IpLatitude = 34.0522, IpLongitude = -118.2437, Latitude = 34.0522, Longitude = -118.2437, City = "Los Angeles", Region = "CA", CountryId = country.Id },
        };
        context.ComputerGeoLocations.AddRange(geoLocations);
        await context.SaveChangesAsync();

        // --- Maintenance records ---
        var maintenanceRecords = new[]
        {
            new MaintenanceRecord { ComputerId = computers[0].Id, TypeId = 1, Description = "Replace battery", ScheduledDate = now.AddDays(7), StatusId = (int)MaintenanceStatus.Scheduled, CreatedBy = "admin" },
            new MaintenanceRecord { ComputerId = computers[0].Id, TypeId = 2, Description = "Upgrade RAM", ScheduledDate = now.AddDays(-5), CompletedDate = now.AddDays(-4), StatusId = (int)MaintenanceStatus.Completed, CreatedBy = "admin" },
            new MaintenanceRecord { ComputerId = computers[2].Id, TypeId = 1, Description = "Fix screen", ScheduledDate = now.AddDays(1), StatusId = (int)MaintenanceStatus.InProgress, CreatedBy = "tech1" },
        };
        context.MaintenanceRecords.AddRange(maintenanceRecords);
        await context.SaveChangesAsync();

        // --- Computer history ---
        var histories = new[]
        {
            new ComputerHistory { ComputerId = computers[0].Id, Action = "Status Changed", OldValue = "Unassigned", NewValue = "Assigned", ChangedBy = "admin", ChangedAt = now.AddDays(-10) },
            new ComputerHistory { ComputerId = computers[0].Id, Action = "Location Changed", OldValue = "Building B", NewValue = "Building A", ChangedBy = "admin", ChangedAt = now.AddDays(-5) },
            new ComputerHistory { ComputerId = computers[2].Id, Action = "Status Changed", OldValue = "Unassigned", NewValue = "InMaintenance", ChangedBy = "tech1", ChangedAt = now.AddDays(-1) },
        };
        context.ComputerHistories.AddRange(histories);
        await context.SaveChangesAsync();

        // --- Users ---
        var users = new[]
        {
            new ApplicationUser { Username = "admin", Email = "admin@test.com", PasswordHash = "$2a$11$test", FirstName = "Admin", LastName = "User", RoleId = 2, DepartmentId = dept.Id, IsActive = true, IsEmailVerified = true, CreatedAt = now },
            new ApplicationUser { Username = "tech1", Email = "tech1@test.com", PasswordHash = "$2a$11$test", FirstName = "Tech", LastName = "One", RoleId = 1, DepartmentId = dept.Id, IsActive = true, IsEmailVerified = true, CreatedAt = now },
            new ApplicationUser { Username = "viewer", Email = "viewer@test.com", PasswordHash = "$2a$11$test", FirstName = "View", LastName = "Only", RoleId = 0, DepartmentId = dept2.Id, IsActive = true, IsEmailVerified = true, CreatedAt = now },
        };
        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        // --- Version history ---
        var versions = new[]
        {
            new VersionHistory { Version = "1.0.0", TypeId = 0, ChangeLog = "Initial release", ReleasedAt = now.AddDays(-60), ReleasedBy = "admin", IsCurrentVersion = false },
            new VersionHistory { Version = "1.1.0", TypeId = 0, ChangeLog = "Bug fixes", ReleasedAt = now.AddDays(-30), ReleasedBy = "admin", IsCurrentVersion = true },
            new VersionHistory { Version = "1.0.0", TypeId = 1, ChangeLog = "ThinClient initial", ReleasedAt = now.AddDays(-60), ReleasedBy = "admin", IsCurrentVersion = true },
        };
        context.VersionHistories.AddRange(versions);
        await context.SaveChangesAsync();
    }

    private static Computer CreateComputer(string serial, string hostname, int modelId, int osId, int typeId, int statusId, int locationId, int deptId, string? ipAddress = null)
    {
        return new Computer
        {
            SerialNumber = serial,
            Hostname = hostname,
            IpAddress = ipAddress ?? $"192.168.1.{Math.Abs(serial.GetHashCode()) % 254 + 1}",
            MacAddress = $"AA:BB:CC:DD:{Math.Abs(serial.GetHashCode()) % 100:D2}:{Math.Abs(serial.GetHashCode()) % 100:D2}",
            ModelId = modelId,
            OperatingSystemId = osId,
            Processor = "Intel Core i7-1185G7",
            RamGB = 16,
            StorageGB = 512,
            TypeId = typeId,
            StatusId = statusId,
            LocationId = locationId,
            DepartmentId = deptId,
            PurchaseDate = DateTime.UtcNow.AddYears(-2),
            WarrantyExpiration = DateTime.UtcNow.AddYears(1),
            PurchasePrice = 1299.99m,
            AssignedTo = "Test User"
        };
    }
}
