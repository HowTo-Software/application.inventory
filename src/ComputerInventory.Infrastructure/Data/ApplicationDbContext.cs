using ComputerInventory.Contracts;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ComputerInventory.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public override int SaveChanges()
    {
        ApplyAuditFields();
        ApplySoftDelete();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditFields();
        ApplySoftDelete();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        ApplySoftDelete();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        ApplySoftDelete();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditFields()
    {
        var username = _currentUserService?.Username;
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IHasCreationAudit>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                // Only set CreatedBy if not already assigned (e.g. agent sets it explicitly)
                if (string.IsNullOrEmpty(entry.Entity.CreatedBy))
                    entry.Entity.CreatedBy = username;
            }
        }

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                // Only set UpdatedBy if not already changed in this unit of work
                if (!entry.Property(nameof(IAuditable.UpdatedBy)).IsModified)
                    entry.Entity.UpdatedBy = username;
            }
        }
    }

    private void ApplySoftDelete()
    {
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = DateTime.UtcNow;
            }
        }
    }

    // === Existing tables ===
    public DbSet<Computer> Computers => Set<Computer>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<ComputerHistory> ComputerHistories => Set<ComputerHistory>();
    public DbSet<SystemDetails> SystemDetails => Set<SystemDetails>();
    public DbSet<RestorePoint> RestorePoints => Set<RestorePoint>();
    public DbSet<VersionHistory> VersionHistories => Set<VersionHistory>();
    public DbSet<ComputerGeoLocation> ComputerGeoLocations => Set<ComputerGeoLocation>();

    // === Lookup tables (3NF) ===
    public DbSet<ComputerTypeLookup> ComputerTypes => Set<ComputerTypeLookup>();
    public DbSet<ComputerStatusLookup> ComputerStatuses => Set<ComputerStatusLookup>();
    public DbSet<UpdateStatusLookup> UpdateStatuses => Set<UpdateStatusLookup>();
    public DbSet<MaintenanceTypeLookup> MaintenanceTypes => Set<MaintenanceTypeLookup>();
    public DbSet<MaintenanceStatusLookup> MaintenanceStatuses => Set<MaintenanceStatusLookup>();
    public DbSet<RestorePointTypeLookup> RestorePointTypes => Set<RestorePointTypeLookup>();
    public DbSet<VersionTypeLookup> VersionTypes => Set<VersionTypeLookup>();
    public DbSet<UserRoleLookup> UserRoles => Set<UserRoleLookup>();

    // === Reference tables (3NF) ===
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<ComputerModel> ComputerModels => Set<ComputerModel>();
    public DbSet<OperatingSystemEntity> OperatingSystems => Set<OperatingSystemEntity>();
    public DbSet<Country> Countries => Set<Country>();

    // === Extracted sub-entities (3NF) ===
    public DbSet<ComputerAgentStatus> ComputerAgentStatuses => Set<ComputerAgentStatus>();
    public DbSet<ComputerUpdateRequest> ComputerUpdateRequests => Set<ComputerUpdateRequest>();
    public DbSet<RestorePointSystemInfo> RestorePointSystemInfos => Set<RestorePointSystemInfo>();
    public DbSet<RestorePointSnapshot> RestorePointSnapshots => Set<RestorePointSnapshot>();
    public DbSet<SystemDetailBios> SystemDetailBios => Set<SystemDetailBios>();
    public DbSet<SystemDetailMotherboard> SystemDetailMotherboards => Set<SystemDetailMotherboard>();
    public DbSet<SystemDetailCpu> SystemDetailCpus => Set<SystemDetailCpu>();
    public DbSet<SystemDetailMemory> SystemDetailMemories => Set<SystemDetailMemory>();
    public DbSet<SystemDetailStorage> SystemDetailStorages => Set<SystemDetailStorage>();
    public DbSet<SystemDetailGpu> SystemDetailGpus => Set<SystemDetailGpu>();
    public DbSet<SystemDetailNetwork> SystemDetailNetworks => Set<SystemDetailNetwork>();
    public DbSet<SystemDetailOs> SystemDetailOses => Set<SystemDetailOs>();
    public DbSet<SystemDetailSecurity> SystemDetailSecurities => Set<SystemDetailSecurity>();
    public DbSet<SystemDetailBattery> SystemDetailBatteries => Set<SystemDetailBattery>();
    public DbSet<SystemDetailChassis> SystemDetailChasses => Set<SystemDetailChassis>();
    public DbSet<SystemDetailSoftware> SystemDetailSoftwares => Set<SystemDetailSoftware>();
    public DbSet<SystemDetailUsersGroups> SystemDetailUsersGroups => Set<SystemDetailUsersGroups>();
    public DbSet<SystemDetailPeripherals> SystemDetailPeripherals => Set<SystemDetailPeripherals>();
    public DbSet<SystemDetailEventLogs> SystemDetailEventLogs => Set<SystemDetailEventLogs>();
    public DbSet<SystemDetailPerformance> SystemDetailPerformances => Set<SystemDetailPerformance>();

    // === Package versioning & distribution ===
    public DbSet<PackageVersion> PackageVersions => Set<PackageVersion>();
    public DbSet<PackageDistributionRule> PackageDistributionRules => Set<PackageDistributionRule>();

    // === Authentication ===
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<InstallToken> InstallTokens => Set<InstallToken>();
    public DbSet<InviteCode> InviteCodes => Set<InviteCode>();

    // === Application settings ===
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    // === Per-computer access control ===
    public DbSet<ComputerOwnership> ComputerOwnerships => Set<ComputerOwnership>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Register all enum-to-int conversions globally so EF Core sees
        // them as int columns before relationship validation runs.
        configurationBuilder.Properties<ComputerType>().HaveConversion<int>();
        configurationBuilder.Properties<ComputerStatus>().HaveConversion<int>();
        configurationBuilder.Properties<UserRole>().HaveConversion<int>();
        configurationBuilder.Properties<MaintenanceType>().HaveConversion<int>();
        configurationBuilder.Properties<MaintenanceStatus>().HaveConversion<int>();
        configurationBuilder.Properties<RestorePointType>().HaveConversion<int>();
        configurationBuilder.Properties<VersionType>().HaveConversion<int>();
        configurationBuilder.Properties<AgentUpdateStatus>().HaveConversion<int>();
        configurationBuilder.Properties<ComputerAccessLevel>().HaveConversion<int>();
        configurationBuilder.Properties<PackageStatus>().HaveConversion<int>();
        configurationBuilder.Properties<DistributionRuleType>().HaveConversion<int>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ================================================================
        // LOOKUP TABLE CONFIGURATIONS & SEED DATA
        // IDs match C# enum integer values for seamless FK↔enum mapping.
        // ================================================================

        modelBuilder.Entity<ComputerTypeLookup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasData(
                new ComputerTypeLookup { Id = 0, Name = "Desktop" },
                new ComputerTypeLookup { Id = 1, Name = "Laptop" },
                new ComputerTypeLookup { Id = 2, Name = "Server" },
                new ComputerTypeLookup { Id = 3, Name = "Workstation" },
                new ComputerTypeLookup { Id = 4, Name = "Tablet" },
                new ComputerTypeLookup { Id = 5, Name = "Other" }
            );
        });

        modelBuilder.Entity<ComputerStatusLookup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasData(
                new ComputerStatusLookup { Id = 0, Name = "Unassigned" },
                new ComputerStatusLookup { Id = 1, Name = "Assigned" },
                new ComputerStatusLookup { Id = 2, Name = "InMaintenance" },
                new ComputerStatusLookup { Id = 3, Name = "Retired" },
                new ComputerStatusLookup { Id = 4, Name = "Lost" },
                new ComputerStatusLookup { Id = 5, Name = "Stolen" },
                new ComputerStatusLookup { Id = 6, Name = "Disposed" }
            );
        });

        modelBuilder.Entity<UpdateStatusLookup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasData(
                new UpdateStatusLookup { Id = 0, Name = "None" },
                new UpdateStatusLookup { Id = 1, Name = "Pending" },
                new UpdateStatusLookup { Id = 2, Name = "CheckingForUpdate" },
                new UpdateStatusLookup { Id = 3, Name = "Downloading" },
                new UpdateStatusLookup { Id = 4, Name = "Verifying" },
                new UpdateStatusLookup { Id = 5, Name = "Extracting" },
                new UpdateStatusLookup { Id = 6, Name = "PreparingUpdater" },
                new UpdateStatusLookup { Id = 7, Name = "StoppingService" },
                new UpdateStatusLookup { Id = 8, Name = "BackingUp" },
                new UpdateStatusLookup { Id = 9, Name = "CopyingFiles" },
                new UpdateStatusLookup { Id = 10, Name = "StartingService" },
                new UpdateStatusLookup { Id = 11, Name = "Completed" },
                new UpdateStatusLookup { Id = 99, Name = "Failed" }
            );
        });

        modelBuilder.Entity<MaintenanceTypeLookup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasData(
                new MaintenanceTypeLookup { Id = 0, Name = "Repair" },
                new MaintenanceTypeLookup { Id = 1, Name = "Upgrade" },
                new MaintenanceTypeLookup { Id = 2, Name = "Cleaning" },
                new MaintenanceTypeLookup { Id = 3, Name = "SoftwareUpdate" },
                new MaintenanceTypeLookup { Id = 4, Name = "HardwareReplacement" },
                new MaintenanceTypeLookup { Id = 5, Name = "Inspection" },
                new MaintenanceTypeLookup { Id = 6, Name = "Other" }
            );
        });

        modelBuilder.Entity<MaintenanceStatusLookup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasData(
                new MaintenanceStatusLookup { Id = 0, Name = "Scheduled" },
                new MaintenanceStatusLookup { Id = 1, Name = "InProgress" },
                new MaintenanceStatusLookup { Id = 2, Name = "Completed" },
                new MaintenanceStatusLookup { Id = 3, Name = "Cancelled" }
            );
        });

        modelBuilder.Entity<RestorePointTypeLookup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasData(
                new RestorePointTypeLookup { Id = 0, Name = "Reboot" },
                new RestorePointTypeLookup { Id = 1, Name = "Scheduled" },
                new RestorePointTypeLookup { Id = 2, Name = "Manual" },
                new RestorePointTypeLookup { Id = 3, Name = "Initial" },
                new RestorePointTypeLookup { Id = 4, Name = "PreChange" },
                new RestorePointTypeLookup { Id = 5, Name = "PostChange" }
            );
        });

        modelBuilder.Entity<VersionTypeLookup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasData(
                new VersionTypeLookup { Id = 0, Name = "Server" },
                new VersionTypeLookup { Id = 1, Name = "ThinClient" }
            );
        });

        modelBuilder.Entity<UserRoleLookup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasData(
                new UserRoleLookup { Id = 0, Name = "Viewer" },
                new UserRoleLookup { Id = 1, Name = "Technician" },
                new UserRoleLookup { Id = 2, Name = "Admin" }
            );
        });

        // ================================================================
        // REFERENCE TABLE CONFIGURATIONS
        // ================================================================
        //
        // CASCADE-DELETE STRATEGY (unified):
        //   Cascade  — aggregate-owned children (child has no meaning without parent)
        //   Restrict — all FK references to lookup / reference / catalog tables
        //   NoAction — circular references only (ComputerAgentStatus ↔ ComputerUpdateRequest)
        //

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Manufacturer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<ComputerModel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ModelName).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => new { e.ManufacturerId, e.ModelName }).IsUnique();
            entity.HasOne(e => e.Manufacturer)
                .WithMany(m => m.Models)
                .HasForeignKey(e => e.ManufacturerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ComputerModelLookupResult>(entity =>
        {
            entity.HasNoKey();
            entity.ToView(null);
        });

        // ================================================================
        // KEYLESS RESULT TYPES (stored procedure report/custom shapes)
        // Mapped to no table or view — used with FromSqlRaw / ExecuteSpListAsync.
        // ================================================================

        modelBuilder.Entity<StatusCountResult>(entity =>
        {
            entity.HasNoKey();
            entity.ToView(null);
        });

        modelBuilder.Entity<ComputerLocationResult>(entity =>
        {
            entity.HasNoKey();
            entity.ToView(null);
        });

        modelBuilder.Entity<RestorePointSummaryItem>(entity =>
        {
            entity.HasNoKey();
            entity.ToView(null);
        });

        modelBuilder.Entity<SystemDetailsReport>(entity =>
        {
            entity.HasNoKey();
            entity.ToView(null);
        });

        modelBuilder.Entity<OperatingSystemEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Country>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CountryCode).IsRequired().HasMaxLength(10);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.CountryCode).IsUnique();
        });

        // ================================================================
        // COMPUTER CONFIGURATION (3NF with FK relationships)
        // ================================================================

        modelBuilder.Entity<Computer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SerialNumber).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.SerialNumber).IsUnique();
            entity.Property(e => e.Hostname).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.MacAddress).HasMaxLength(17);
            entity.Property(e => e.Processor).HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.PurchasePrice).HasPrecision(18, 2);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            entity.Property(e => e.AssignedTo).HasMaxLength(200);

            // Concurrency token
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            // Enum-to-int conversions (must be declared before FK relationships)
            entity.Property(e => e.TypeId).HasConversion<int>();
            entity.Property(e => e.StatusId).HasConversion<int>();

            // FK relationships (3NF)
            entity.HasOne(e => e.ComputerModel)
                .WithMany()
                .HasForeignKey(e => e.ModelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.OperatingSystemEntity)
                .WithMany()
                .HasForeignKey(e => e.OperatingSystemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TypeLookup)
                .WithMany()
                .HasForeignKey(e => e.TypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.StatusLookup)
                .WithMany()
                .HasForeignKey(e => e.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.LocationEntity)
                .WithMany()
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.DepartmentEntity)
                .WithMany()
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Performance indexes
            entity.HasIndex(e => e.Hostname);
            entity.HasIndex(e => e.StatusId);
            entity.HasIndex(e => e.DepartmentId);
            entity.HasIndex(e => e.LocationId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // ================================================================
        // COMPUTER AGENT STATUS (extracted from Computer — 1:1)
        // ================================================================

        modelBuilder.Entity<ComputerAgentStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AgentVersion).HasMaxLength(50);
            entity.Property(e => e.AgentSessionId).HasMaxLength(64);

            entity.HasOne(e => e.Computer)
                .WithOne(c => c.AgentStatus)
                .HasForeignKey<ComputerAgentStatus>(e => e.ComputerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ActiveUpdateRequest)
                .WithMany()
                .HasForeignKey(e => e.ActiveUpdateRequestId)
                .OnDelete(DeleteBehavior.NoAction);

            // Performance indexes
            entity.HasIndex(e => e.ComputerId).IsUnique();
            entity.HasIndex(e => e.LastSeen);
            entity.HasIndex(e => e.IsOnline);
            entity.HasIndex(e => e.HasAgent);
        });

        // ================================================================
        // COMPUTER UPDATE REQUEST (extracted from Computer)
        // ================================================================

        modelBuilder.Entity<ComputerUpdateRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UpdateStatusMessage).HasMaxLength(500);
            entity.Property(e => e.UpdateRequestedBy).HasMaxLength(100);
            entity.Property(e => e.PreviousAgentVersion).HasMaxLength(50);

            entity.HasOne(e => e.Computer)
                .WithMany()
                .HasForeignKey(e => e.ComputerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.UpdateStatusLookup)
                .WithMany()
                .HasForeignKey(e => e.UpdateStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.UpdateStatusId).HasConversion<int>();

            entity.HasIndex(e => e.ComputerId);
            entity.HasIndex(e => new { e.ComputerId, e.CreatedAt });
            entity.HasIndex(e => e.UpdateStatusId).HasFilter("[update_status_id] > 0");
        });

        // ================================================================
        // APPLICATION USER (3NF with FK relationships)
        // ================================================================

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.EmailVerificationToken).HasMaxLength(128);

            entity.HasIndex(e => e.DepartmentId);

            entity.HasOne(e => e.DepartmentEntity)
                .WithMany()
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.RoleId);

            entity.HasOne(e => e.RoleLookup)
                .WithMany()
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.RoleId).HasConversion<int>();
        });

        // ================================================================
        // COMPUTER OWNERSHIP (per-computer access control)
        // ================================================================

        modelBuilder.Entity<ComputerOwnership>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GrantedBy).HasMaxLength(100);

            entity.HasOne(e => e.Computer)
                .WithMany()
                .HasForeignKey(e => e.ComputerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.AccessLevel).HasConversion<int>();

            // Each user can only have one ownership record per computer
            entity.HasIndex(e => new { e.ComputerId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        // ================================================================
        // MAINTENANCE RECORD (3NF with FK relationships)
        // ================================================================

        modelBuilder.Entity<MaintenanceRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Cost).HasPrecision(18, 2);
            entity.Property(e => e.PerformedBy).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);

            entity.HasOne(e => e.Computer)
                .WithMany(c => c.MaintenanceRecords)
                .HasForeignKey(e => e.ComputerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.TypeLookup)
                .WithMany()
                .HasForeignKey(e => e.TypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.TypeId).HasConversion<int>();

            entity.HasOne(e => e.StatusLookup)
                .WithMany()
                .HasForeignKey(e => e.StatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.StatusId).HasConversion<int>();

            entity.HasIndex(e => e.ComputerId);
            entity.HasIndex(e => new { e.ComputerId, e.ScheduledDate });
            entity.HasIndex(e => e.StatusId);
            entity.HasIndex(e => e.ScheduledDate);

            // Concurrency token
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        // ================================================================
        // COMPUTER HISTORY
        // ================================================================

        modelBuilder.Entity<ComputerHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(100);
            entity.Property(e => e.OldValue).HasMaxLength(500);
            entity.Property(e => e.NewValue).HasMaxLength(500);
            entity.Property(e => e.ChangedBy).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(500);

            entity.HasOne(e => e.Computer)
                .WithMany(c => c.History)
                .HasForeignKey(e => e.ComputerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ComputerId, e.ChangedAt });
            entity.HasIndex(e => e.ChangedAt);
            entity.HasIndex(e => e.Action);

            // Concurrency token
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        // ================================================================
        // SYSTEM DETAILS (3NF parent + 16 sub-entities)
        // ================================================================

        modelBuilder.Entity<SystemDetails>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Computer)
                .WithMany()
                .HasForeignKey(e => e.ComputerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.ComputerId, e.CollectedAt });

            // Concurrency token
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });

        // SystemDetail sub-entities: 1:1 with SystemDetails
        ConfigureSystemDetailSubEntity<SystemDetailBios>(modelBuilder, e => e.Bios);
        ConfigureSystemDetailSubEntity<SystemDetailMotherboard>(modelBuilder, e => e.Motherboard);
        ConfigureSystemDetailSubEntity<SystemDetailCpu>(modelBuilder, e => e.Cpu);
        ConfigureSystemDetailSubEntity<SystemDetailMemory>(modelBuilder, e => e.Memory);
        ConfigureSystemDetailSubEntity<SystemDetailStorage>(modelBuilder, e => e.Storage);
        ConfigureSystemDetailSubEntity<SystemDetailGpu>(modelBuilder, e => e.Gpu);
        ConfigureSystemDetailSubEntity<SystemDetailNetwork>(modelBuilder, e => e.Network);
        ConfigureSystemDetailSubEntity<SystemDetailOs>(modelBuilder, e => e.Os);
        ConfigureSystemDetailSubEntity<SystemDetailSecurity>(modelBuilder, e => e.Security);
        ConfigureSystemDetailSubEntity<SystemDetailBattery>(modelBuilder, e => e.Battery);
        ConfigureSystemDetailSubEntity<SystemDetailChassis>(modelBuilder, e => e.Chassis);
        ConfigureSystemDetailSubEntity<SystemDetailSoftware>(modelBuilder, e => e.Software);
        ConfigureSystemDetailSubEntity<SystemDetailUsersGroups>(modelBuilder, e => e.UsersGroups);
        ConfigureSystemDetailSubEntity<SystemDetailPeripherals>(modelBuilder, e => e.Peripherals);
        ConfigureSystemDetailSubEntity<SystemDetailEventLogs>(modelBuilder, e => e.EventLogs);
        ConfigureSystemDetailSubEntity<SystemDetailPerformance>(modelBuilder, e => e.Performance);

        // ================================================================
        // RESTORE POINT (3NF with sub-entities)
        // ================================================================

        modelBuilder.Entity<RestorePoint>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RestorePointId).IsUnique();
            entity.HasIndex(e => new { e.ComputerId, e.CreatedAt });
            entity.HasIndex(e => new { e.ComputerId, e.TypeId });

            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DataHash).HasMaxLength(128);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);

            entity.HasOne(e => e.Computer)
                .WithMany()
                .HasForeignKey(e => e.ComputerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.TypeLookup)
                .WithMany()
                .HasForeignKey(e => e.TypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.TypeId).HasConversion<int>();
        });

        // RestorePoint sub-entities: 1:1
        modelBuilder.Entity<RestorePointSystemInfo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Hostname).HasMaxLength(100);
            entity.Property(e => e.SerialNumber).HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.MacAddress).HasMaxLength(17);
            entity.Property(e => e.Manufacturer).HasMaxLength(100);
            entity.Property(e => e.Model).HasMaxLength(100);
            entity.Property(e => e.CurrentUser).HasMaxLength(100);
            entity.HasOne(e => e.RestorePoint)
                .WithOne(rp => rp.SystemInfo)
                .HasForeignKey<RestorePointSystemInfo>(e => e.RestorePointId)
                .OnDelete(DeleteBehavior.Cascade);

            // Match the RestorePoint soft-delete query filter
            entity.HasQueryFilter(e => !e.RestorePoint.IsDeleted);
        });

        modelBuilder.Entity<RestorePointSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.RestorePoint)
                .WithOne(rp => rp.Snapshot)
                .HasForeignKey<RestorePointSnapshot>(e => e.RestorePointId)
                .OnDelete(DeleteBehavior.Cascade);

            // Match the RestorePoint soft-delete query filter
            entity.HasQueryFilter(e => !e.RestorePoint.IsDeleted);

            // GZip-compress all JSON snapshot columns to reduce database bloat.
            // Stored as varbinary(max); transparently decompressed on read.
            var gzipConverter = new GZipCompressedStringConverter();
            entity.Property(e => e.SystemDetailsJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.InstalledSoftwareJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.RunningProcessesJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.WindowsServicesJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.StartupProgramsJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.ScheduledTasksJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.EnvironmentVariablesJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.NetworkConfigJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.StorageConfigJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.EventLogSummaryJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.WindowsUpdatesJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.DeviceDriversJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.FirewallRulesJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.LocalUsersJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.LocalGroupsJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.SharedFoldersJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.PrintersJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.UsbDevicesJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.BatteryStatusJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.BitLockerStatusJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
            entity.Property(e => e.SecurityStatusJson).HasConversion(gzipConverter).HasColumnType("varbinary(max)");
        });

        // ================================================================
        // VERSION HISTORY (3NF with FK to lookup)
        // ================================================================

        modelBuilder.Entity<VersionHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Version).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ChangeLog).IsRequired();
            entity.Property(e => e.ReleasedBy).HasMaxLength(100);
            entity.Property(e => e.Checksum).HasMaxLength(128);

            entity.HasOne(e => e.TypeLookup)
                .WithMany()
                .HasForeignKey(e => e.TypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.TypeId).HasConversion<int>();

            entity.HasIndex(e => new { e.TypeId, e.Version }).IsUnique();
            entity.HasIndex(e => new { e.TypeId, e.IsCurrentVersion });
            entity.HasIndex(e => e.ReleasedAt);
        });

        // ================================================================
        // GEO LOCATION (3NF with FK to Countries)
        // ================================================================

        modelBuilder.Entity<ComputerGeoLocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PublicIpAddress).HasMaxLength(45);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.Region).HasMaxLength(100);
            entity.Property(e => e.Isp).HasMaxLength(200);
            entity.Property(e => e.Timezone).HasMaxLength(50);
            entity.Property(e => e.LookupError).HasMaxLength(500);

            entity.HasOne(e => e.CountryEntity)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ComputerId).IsUnique();
            entity.HasIndex(e => e.LastUpdated);
            entity.HasIndex(e => e.CountryId);

            entity.HasOne(e => e.Computer)
                .WithOne(c => c.GeoLocation)
                .HasForeignKey<ComputerGeoLocation>(e => e.ComputerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ================================================================
        // PACKAGE VERSIONING & DISTRIBUTION
        // ================================================================

        modelBuilder.Entity<PackageVersion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Version).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PackageType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.StoragePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Checksum).IsRequired().HasMaxLength(128);
            entity.Property(e => e.ReleaseNotes).HasColumnType("nvarchar(max)");
            entity.Property(e => e.MinimumAgentVersion).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.Channel).IsRequired().HasMaxLength(20).HasDefaultValue("stable");

            entity.Property(e => e.Status).HasConversion<int>();

            entity.HasIndex(e => new { e.Version, e.PackageType }).IsUnique();
            entity.HasIndex(e => new { e.Status, e.PackageType });
            entity.HasIndex(e => new { e.Channel, e.PackageType });
            entity.HasIndex(e => e.CreatedAt);

            entity.HasMany(e => e.DistributionRules)
                .WithOne(r => r.PackageVersion)
                .HasForeignKey(r => r.PackageVersionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PackageDistributionRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TargetValue).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RuleType).HasConversion<int>();

            entity.HasIndex(e => e.PackageVersionId);
        });

        // ================================================================
        // REFRESH TOKENS
        // ================================================================

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Family).IsRequired().HasMaxLength(64);

            entity.HasIndex(e => e.TokenHash);
            entity.HasIndex(e => e.Family);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ================================================================
        // APP SETTINGS (key-value store)
        // ================================================================

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(128);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(1024);
        });

        // ================================================================
        // INSTALL TOKENS (single-use, time-limited)
        // ================================================================

        modelBuilder.Entity<InstallToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(128);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.TokenHash);
        });

        // ================================================================
        // INVITE CODES (registration gate)
        // ================================================================

        modelBuilder.Entity<InviteCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CodeHash).IsRequired().HasMaxLength(128);
            entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.HasIndex(e => e.CodeHash);
        });

        // Admin user must be seeded manually via the DDL script

        // ================================================================
        // AUDIT FIELD DEFAULTS
        // Apply GETUTCDATE() default for all CreatedAt columns so the
        // database stamps new rows automatically when code doesn't set it.
        // ================================================================

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var createdAt = entityType.FindProperty("CreatedAt");
            if (createdAt != null && createdAt.ClrType == typeof(DateTime))
            {
                createdAt.SetDefaultValueSql("GETUTCDATE()");
            }
        }

        // ================================================================
        // SOFT-DELETE GLOBAL QUERY FILTERS
        // Automatically exclude soft-deleted rows from all queries.
        // Use .IgnoreQueryFilters() to include deleted records when needed.
        // ================================================================

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                var property = System.Linq.Expressions.Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
                var falseConstant = System.Linq.Expressions.Expression.Constant(false);
                var filter = System.Linq.Expressions.Expression.Equal(property, falseConstant);
                var lambda = System.Linq.Expressions.Expression.Lambda(filter, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    /// <summary>
    /// Helper to configure 1:1 SystemDetail sub-entity relationships.
    /// </summary>
    private static void ConfigureSystemDetailSubEntity<TSubEntity>(
        ModelBuilder modelBuilder,
        System.Linq.Expressions.Expression<Func<SystemDetails, TSubEntity?>> navigationExpression)
        where TSubEntity : class
    {
        modelBuilder.Entity<TSubEntity>(entity =>
        {
            entity.HasKey("Id");
            entity.HasOne<SystemDetails>("SystemDetail")
                .WithOne(navigationExpression)
                .HasForeignKey<TSubEntity>("SystemDetailId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Add matching query filter so EF doesn't warn about the required end
        // (SystemDetails) being filtered while the dependent is not.
        var param = System.Linq.Expressions.Expression.Parameter(typeof(TSubEntity), "e");
        var nav = System.Linq.Expressions.Expression.Property(param, "SystemDetail");
        var isDeleted = System.Linq.Expressions.Expression.Property(nav, nameof(ISoftDeletable.IsDeleted));
        var notDeleted = System.Linq.Expressions.Expression.Not(isDeleted);
        var lambda = System.Linq.Expressions.Expression.Lambda(notDeleted, param);
        modelBuilder.Entity<TSubEntity>().HasQueryFilter(lambda);
    }
}
