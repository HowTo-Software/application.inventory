using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ComputerInventory.Infrastructure.Data;

/// <summary>
/// Extension methods for calling stored procedures via EF Core.
/// Provides a standardized convention for <c>FromSqlRaw</c> / <c>ExecuteSqlRawAsync</c> calls.
/// </summary>
/// <example>
/// <code>
/// // Query returning entities:
/// var computers = await _context.ExecuteSpListAsync&lt;Computer&gt;(
///     "sp_search_computers", cancellationToken,
///     SpParam.Input("@search_term", searchTerm));
///
/// // Single entity:
/// var computer = await _context.ExecuteSpSingleAsync&lt;Computer&gt;(
///     "sp_get_computer_by_id", cancellationToken,
///     SpParam.Input("@id", computerId));
///
/// // Non-query (INSERT/UPDATE/DELETE):
/// var affected = await _context.ExecuteSpNonQueryAsync(
///     "sp_mark_offline_computers", cancellationToken,
///     SpParam.Input("@offline_threshold_minutes", 30));
///
/// // Scalar value:
/// var count = await _context.ExecuteSpScalarAsync&lt;int&gt;(
///     "sp_get_total_computer_count", cancellationToken);
///
/// // Output parameter:
/// var idParam = SpParam.Output("@id", SqlDbType.Int);
/// await _context.ExecuteSpNonQueryAsync(
///     "sp_find_or_create_manufacturer", cancellationToken,
///     SpParam.Input("@name", "Dell"), idParam);
/// var manufacturerId = (int)idParam.Value!;
/// </code>
/// </example>
public static partial class StoredProcedureExtensions
{
    /// <summary>
    /// Executes a stored procedure that returns a list of entities.
    /// The entity type <typeparamref name="T"/> must be registered in the <see cref="DbContext"/>.
    /// </summary>
    public static async Task<List<T>> ExecuteSpListAsync<T>(
        this DbContext context,
        string procedureName,
        CancellationToken cancellationToken = default,
        params SqlParameter[] parameters) where T : class
    {
        var (sql, prms) = BuildSpCall(procedureName, parameters);
        return await context.Set<T>()
            .FromSqlRaw(sql, prms)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Executes a stored procedure that returns a single entity or <c>null</c>.
    /// The entity type <typeparamref name="T"/> must be registered in the <see cref="DbContext"/>.
    /// </summary>
    public static async Task<T?> ExecuteSpSingleAsync<T>(
        this DbContext context,
        string procedureName,
        CancellationToken cancellationToken = default,
        params SqlParameter[] parameters) where T : class
    {
        var (sql, prms) = BuildSpCall(procedureName, parameters);
        var list = await context.Set<T>()
            .FromSqlRaw(sql, prms)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return list.FirstOrDefault();
    }

    /// <summary>
    /// Executes a stored procedure that returns a single <b>tracked</b> entity or <c>null</c>.
    /// Use this when the caller needs to modify the entity and later call <c>SaveChangesAsync</c>.
    /// </summary>
    public static async Task<T?> ExecuteSpSingleTrackedAsync<T>(
        this DbContext context,
        string procedureName,
        CancellationToken cancellationToken = default,
        params SqlParameter[] parameters) where T : class
    {
        var (sql, prms) = BuildSpCall(procedureName, parameters);
        var list = await context.Set<T>()
            .FromSqlRaw(sql, prms)
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);
        return list.FirstOrDefault();
    }

    /// <summary>
    /// Executes a stored procedure that modifies data (INSERT/UPDATE/DELETE).
    /// Returns the number of rows affected.
    /// </summary>
    public static async Task<int> ExecuteSpNonQueryAsync(
        this DbContext context,
        string procedureName,
        CancellationToken cancellationToken = default,
        params SqlParameter[] parameters)
    {
        var (sql, prms) = BuildSpCall(procedureName, parameters);
        return await context.Database.ExecuteSqlRawAsync(sql, prms, cancellationToken);
    }

    /// <summary>
    /// Executes a stored procedure that returns a scalar value (first column of the first row).
    /// </summary>
    public static async Task<T?> ExecuteSpScalarAsync<T>(
        this DbContext context,
        string procedureName,
        CancellationToken cancellationToken = default,
        params SqlParameter[] parameters)
    {
        var (sql, prms) = BuildSpCall(procedureName, parameters);
        var connection = context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;

        if (!wasOpen)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddRange(prms);

            if (context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null or DBNull)
                return default;

            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            return (T)Convert.ChangeType(result, targetType);
        }
        finally
        {
            if (!wasOpen)
                await connection.CloseAsync();
        }
    }

    private static (string Sql, SqlParameter[] Parameters) BuildSpCall(
        string procedureName,
        SqlParameter[] parameters)
    {
        ValidateProcedureName(procedureName);

        if (parameters.Length == 0)
            return ($"EXEC [{procedureName}]", []);

        var paramList = string.Join(", ", parameters.Select(p =>
            p.Direction is ParameterDirection.Output or ParameterDirection.InputOutput
                ? $"{p.ParameterName} OUTPUT"
                : p.ParameterName));

        return ($"EXEC [{procedureName}] {paramList}", parameters);
    }

    private static void ValidateProcedureName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!SpNameRegex().IsMatch(name))
            throw new ArgumentException(
                $"Invalid stored procedure name: '{name}'. Only letters, digits, and underscores are allowed.",
                nameof(name));
    }

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]{0,127}$")]
    private static partial Regex SpNameRegex();
}

/// <summary>
/// Factory methods for creating <see cref="SqlParameter"/> instances in stored procedure calls.
/// </summary>
public static class SpParam
{
    /// <summary>Creates an input parameter.</summary>
    public static SqlParameter Input(string name, object? value) =>
        new(EnsurePrefix(name), value ?? DBNull.Value);

    /// <summary>Creates an input parameter with an explicit SQL type.</summary>
    public static SqlParameter Input(string name, object? value, SqlDbType type) =>
        new(EnsurePrefix(name), type) { Value = value ?? DBNull.Value };

    /// <summary>Creates an output parameter.</summary>
    public static SqlParameter Output(string name, SqlDbType type) =>
        new(EnsurePrefix(name), type) { Direction = ParameterDirection.Output };

    /// <summary>Creates an input/output parameter.</summary>
    public static SqlParameter InputOutput(string name, object? value, SqlDbType type) =>
        new(EnsurePrefix(name), type)
        {
            Value = value ?? DBNull.Value,
            Direction = ParameterDirection.InputOutput
        };

    private static string EnsurePrefix(string name) =>
        name.StartsWith('@') ? name : $"@{name}";
}
