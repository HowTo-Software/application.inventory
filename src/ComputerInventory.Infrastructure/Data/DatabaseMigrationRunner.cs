using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ComputerInventory.Infrastructure.Data;

/// <summary>
/// Applies SQL migration scripts from a configured directory at application startup.
/// Tracks applied migrations in a <c>__migration_history</c> table to ensure each script runs exactly once.
/// Scripts are processed in filename order (e.g. <c>001_Initial.sql</c>, <c>002_AddColumn.sql</c>).
/// <c>GO</c> batch separators are handled automatically.
/// </summary>
public sealed class DatabaseMigrationRunner
{
    private readonly ILogger<DatabaseMigrationRunner> _logger;

    public DatabaseMigrationRunner(ILogger<DatabaseMigrationRunner> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Scans <paramref name="migrationsPath"/> for <c>*.sql</c> files and applies any that have not yet been recorded
    /// in the <c>__migration_history</c> tracking table.
    /// </summary>
    public async Task ApplyMigrationsAsync(DbContext context, string migrationsPath, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(migrationsPath))
        {
            _logger.LogWarning("Migrations directory not found at {Path} — skipping automatic migrations", migrationsPath);
            return;
        }

        var connectionString = context.Database.GetConnectionString();
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("No database connection string available — skipping automatic migrations");
            return;
        }

        // Use a dedicated connection so we don't interfere with the EF Core connection pool.
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureHistoryTableAsync(connection, cancellationToken);

        var applied = await GetAppliedMigrationsAsync(connection, cancellationToken);

        var files = Directory.GetFiles(migrationsPath, "*.sql")
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var pending = files.Where(f => !applied.Contains(Path.GetFileName(f))).ToList();

        if (pending.Count == 0)
        {
            _logger.LogInformation("Database is up to date — all {Count} migrations already applied", files.Count);
            return;
        }

        _logger.LogInformation("Found {Pending} pending migration(s) out of {Total} total", pending.Count, files.Count);

        foreach (var file in pending)
        {
            var fileName = Path.GetFileName(file);
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Applying migration: {Migration}", fileName);

            try
            {
                var sql = await File.ReadAllTextAsync(file, cancellationToken);
                await ExecuteBatchesAsync(connection, sql, cancellationToken);
                await RecordMigrationAsync(connection, fileName, cancellationToken);

                _logger.LogInformation("Successfully applied migration: {Migration}", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply migration {Migration} — stopping migration runner", fileName);
                throw;
            }
        }

        _logger.LogInformation("All {Count} pending migration(s) applied successfully", pending.Count);
    }

    /// <summary>
    /// Creates the tracking table if it does not already exist.
    /// </summary>
    private static async Task EnsureHistoryTableAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '__migration_history')
            BEGIN
                CREATE TABLE [dbo].[__migration_history] (
                    [id]           INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [migration]    NVARCHAR(256)  NOT NULL,
                    [applied_at]   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT [UQ_migration_history_migration] UNIQUE ([migration])
                );
            END
            """;

        await using var cmd = new SqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the set of migration file names that have already been applied.
    /// </summary>
    private static async Task<HashSet<string>> GetAppliedMigrationsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT [migration] FROM [dbo].[__migration_history]";

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new SqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            set.Add(reader.GetString(0));
        }
        return set;
    }

    /// <summary>
    /// Splits a SQL script on <c>GO</c> batch separators and executes each batch individually.
    /// </summary>
    private static async Task ExecuteBatchesAsync(SqlConnection connection, string script, CancellationToken cancellationToken)
    {
        var batches = SplitOnGo(script);

        foreach (var batch in batches)
        {
            if (string.IsNullOrWhiteSpace(batch))
                continue;

            await using var cmd = new SqlCommand(batch, connection) { CommandTimeout = 120 };
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Splits a SQL script on lines that contain only <c>GO</c> (case-insensitive), which is the standard
    /// batch separator used by SQL Server Management Studio and <c>sqlcmd</c>.
    /// </summary>
    private static List<string> SplitOnGo(string script)
    {
        var batches = new List<string>();
        var current = new System.Text.StringBuilder();

        using var reader = new StringReader(script);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                batches.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.AppendLine(line);
            }
        }

        // Add remaining text after the last GO (or all text if no GO was present)
        if (current.Length > 0)
            batches.Add(current.ToString());

        return batches;
    }

    /// <summary>
    /// Records a successfully applied migration in the tracking table.
    /// </summary>
    private static async Task RecordMigrationAsync(SqlConnection connection, string migrationName, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO [dbo].[__migration_history] ([migration]) VALUES (@migration)";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add("@migration", SqlDbType.NVarChar, 256).Value = migrationName;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
