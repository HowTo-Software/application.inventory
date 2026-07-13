using System.Linq.Expressions;
using ComputerInventory.Contracts;
using ComputerInventory.Core.Entities;

namespace ComputerInventory.Infrastructure.Sorting;

/// <summary>
/// Provides type-safe, whitelist-based sorting for IQueryable.
/// Each entity defines its own allowed sort fields mapped to strongly-typed expressions.
/// </summary>
public static class SortExtensions
{
    private static readonly Dictionary<string, Expression<Func<Computer, object?>>> ComputerSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hostname"] = c => c.Hostname,
        ["serialnumber"] = c => c.SerialNumber,
        ["ipaddress"] = c => c.IpAddress,
        ["status"] = c => c.StatusLookup!.Name,
        ["type"] = c => c.TypeLookup!.Name,
        ["location"] = c => c.LocationEntity!.Name,
        ["department"] = c => c.DepartmentEntity!.Name,
        ["manufacturer"] = c => c.ComputerModel!.Manufacturer!.Name,
        ["model"] = c => c.ComputerModel!.ModelName,
        ["assignedto"] = c => c.AssignedTo,
        ["updatedat"] = c => c.UpdatedAt,
        ["lastseen"] = c => c.AgentStatus.LastSeen,
        ["createdat"] = c => c.CreatedAt,
        ["agentversion"] = c => c.AgentStatus.AgentVersion,
        ["isonline"] = c => c.AgentStatus.IsOnline,
    };

    private static readonly Dictionary<string, Expression<Func<MaintenanceRecord, object?>>> MaintenanceSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["scheduleddate"] = m => m.ScheduledDate,
        ["completeddate"] = m => m.CompletedDate,
        ["type"] = m => m.TypeLookup!.Name,
        ["status"] = m => m.StatusLookup!.Name,
        ["computer"] = m => m.Computer!.Hostname,
        ["cost"] = m => m.Cost,
        ["performedby"] = m => m.PerformedBy,
        ["createdat"] = m => m.CreatedAt,
    };

    private static readonly Dictionary<string, Expression<Func<ComputerHistory, object?>>> AuditLogSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["changedat"] = h => h.ChangedAt,
        ["action"] = h => h.Action,
        ["changedby"] = h => h.ChangedBy,
        ["computer"] = h => h.Computer!.Hostname,
    };

    private static readonly Dictionary<string, Expression<Func<ApplicationUser, object?>>> UserSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ["username"] = u => u.Username,
        ["email"] = u => u.Email,
        ["firstname"] = u => u.FirstName,
        ["lastname"] = u => u.LastName,
        ["fullname"] = u => u.LastName,
        ["role"] = u => u.RoleLookup!.Name,
        ["department"] = u => u.DepartmentEntity!.Name,
        ["isactive"] = u => u.IsActive,
        ["createdat"] = u => u.CreatedAt,
        ["lastloginat"] = u => u.LastLoginAt,
    };

    public static IOrderedQueryable<Computer> ApplySort(
        this IQueryable<Computer> query,
        string? sortBy,
        SortDirection sortDirection = SortDirection.Asc)
    {
        return ApplySortInternal(query, sortBy, sortDirection, ComputerSortFields, c => c.Hostname);
    }

    public static IOrderedQueryable<MaintenanceRecord> ApplySort(
        this IQueryable<MaintenanceRecord> query,
        string? sortBy,
        SortDirection sortDirection = SortDirection.Desc)
    {
        return ApplySortInternal(query, sortBy, sortDirection, MaintenanceSortFields, m => m.ScheduledDate);
    }

    public static IOrderedQueryable<ComputerHistory> ApplySort(
        this IQueryable<ComputerHistory> query,
        string? sortBy,
        SortDirection sortDirection = SortDirection.Desc)
    {
        return ApplySortInternal(query, sortBy, sortDirection, AuditLogSortFields, h => h.ChangedAt);
    }

    public static IOrderedQueryable<ApplicationUser> ApplySort(
        this IQueryable<ApplicationUser> query,
        string? sortBy,
        SortDirection sortDirection = SortDirection.Asc)
    {
        return ApplySortInternal(query, sortBy, sortDirection, UserSortFields, u => u.LastName);
    }

    private static IOrderedQueryable<T> ApplySortInternal<T, TDefault>(
        IQueryable<T> query,
        string? sortBy,
        SortDirection sortDirection,
        Dictionary<string, Expression<Func<T, object?>>> fieldMap,
        Expression<Func<T, TDefault>> defaultSort)
    {
        if (string.IsNullOrWhiteSpace(sortBy) || !fieldMap.TryGetValue(sortBy, out var sortExpression))
        {
            // Fall back to default sort
            return sortDirection == SortDirection.Desc
                ? query.OrderByDescending(defaultSort)
                : query.OrderBy(defaultSort);
        }

        return sortDirection == SortDirection.Desc
            ? query.OrderByDescending(sortExpression)
            : query.OrderBy(sortExpression);
    }
}
