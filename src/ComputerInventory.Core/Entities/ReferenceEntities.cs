using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ComputerInventory.Core.Interfaces;

namespace ComputerInventory.Core.Entities;

// ============================================================================
// Normalized Reference Table Entities (3NF)
// These eliminate duplicate string data across the database.
// ============================================================================

/// <summary>
/// Departments table (eliminates duplicate department strings in Computers and Users).
/// Maps to DEPARTMENTS table.
/// </summary>
public class Department : ILookupEntity
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Locations table (eliminates duplicate location strings in Computers).
/// Maps to LOCATIONS table.
/// </summary>
public class Location : ILookupEntity
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Manufacturers table (extracted to break Model → Manufacturer transitive dependency).
/// Maps to MANUFACTURERS table.
/// </summary>
public class Manufacturer : ILookupEntity
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation
    [InverseProperty(nameof(ComputerModel.Manufacturer))]
    public ICollection<ComputerModel> Models { get; set; } = new List<ComputerModel>();
}

/// <summary>
/// Computer models with FK to Manufacturer.
/// Maps to COMPUTER_MODELS table.
/// </summary>
public class ComputerModel
{
    public int Id { get; set; }
    public int ManufacturerId { get; set; }

    [Required]
    [StringLength(100)]
    public string ModelName { get; set; } = string.Empty;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation
    [InverseProperty(nameof(Entities.Manufacturer.Models))]
    public Manufacturer Manufacturer { get; set; } = null!;
}

/// <summary>
/// Keyless result type for <c>sp_get_computer_models</c>.
/// </summary>
public class ComputerModelLookupResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ManufacturerId { get; set; }
    public string ManufacturerName { get; set; } = string.Empty;
}

/// <summary>
/// Operating systems table (eliminates duplicate OS strings).
/// Maps to OPERATING_SYSTEMS table.
/// </summary>
public class OperatingSystemEntity : ILookupEntity
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Countries table (eliminates Country → CountryCode transitive dependency).
/// Maps to COUNTRIES table.
/// </summary>
public class Country : ILookupEntity
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string CountryCode { get; set; } = string.Empty;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
