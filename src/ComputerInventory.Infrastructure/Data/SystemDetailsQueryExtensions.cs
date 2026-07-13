using ComputerInventory.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComputerInventory.Infrastructure.Data;

/// <summary>
/// IQueryable extensions for SystemDetails to encapsulate the 16 sub-entity includes
/// into logical domain groups, eliminating repetitive Include chains across repositories.
/// </summary>
public static class SystemDetailsQueryExtensions
{
    /// <summary>
    /// Includes all 16 sub-entities using a split query. Use for full detail views.
    /// </summary>
    public static IQueryable<SystemDetails> IncludeAllSubEntities(this IQueryable<SystemDetails> query)
    {
        return query
            .IncludeHardware()
            .IncludeOperatingSystem()
            .IncludeSoftwareAndUsers()
            .IncludeDiagnostics()
            .AsSplitQuery();
    }

    /// <summary>
    /// Includes physical hardware sub-entities: Bios, Motherboard, CPU, Memory, Storage, GPU, Battery, Chassis.
    /// </summary>
    public static IQueryable<SystemDetails> IncludeHardware(this IQueryable<SystemDetails> query)
    {
        return query
            .Include(d => d.Bios)
            .Include(d => d.Motherboard)
            .Include(d => d.Cpu)
            .Include(d => d.Memory)
            .Include(d => d.Storage)
            .Include(d => d.Gpu)
            .Include(d => d.Battery)
            .Include(d => d.Chassis);
    }

    /// <summary>
    /// Includes OS, security, and network sub-entities.
    /// </summary>
    public static IQueryable<SystemDetails> IncludeOperatingSystem(this IQueryable<SystemDetails> query)
    {
        return query
            .Include(d => d.Os)
            .Include(d => d.Security)
            .Include(d => d.Network);
    }

    /// <summary>
    /// Includes installed software and user/group sub-entities.
    /// </summary>
    public static IQueryable<SystemDetails> IncludeSoftwareAndUsers(this IQueryable<SystemDetails> query)
    {
        return query
            .Include(d => d.Software)
            .Include(d => d.UsersGroups);
    }

    /// <summary>
    /// Includes runtime diagnostics sub-entities: Performance, EventLogs, Peripherals.
    /// </summary>
    public static IQueryable<SystemDetails> IncludeDiagnostics(this IQueryable<SystemDetails> query)
    {
        return query
            .Include(d => d.Performance)
            .Include(d => d.EventLogs)
            .Include(d => d.Peripherals);
    }
}
