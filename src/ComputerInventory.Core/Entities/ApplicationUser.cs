using System.ComponentModel.DataAnnotations;
using ComputerInventory.Core.Interfaces;

namespace ComputerInventory.Core.Entities;

public class ApplicationUser : ISoftDeletable
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;
    
    // FK to DEPARTMENTS
    public int? DepartmentId { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }
    
    // FK to USER_ROLES lookup
    public int RoleId { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Email Verification
    public bool IsEmailVerified { get; set; }

    [StringLength(128)]
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiry { get; set; }
    
    // Audit Fields
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    
    // Soft-delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Navigation Properties
    public Department? DepartmentEntity { get; set; }
    public UserRoleLookup RoleLookup { get; set; } = null!;
    
    public string FullName => $"{FirstName} {LastName}";
}

public enum UserRole
{
    Viewer = 0,      // Can only view inventory
    Technician = 1,  // Can view and edit inventory
    Admin = 2        // Full access including user management
}
