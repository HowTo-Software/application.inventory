using ComputerInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace ComputerInventory.Tests.Integration;

/// <summary>
/// Shared fixture that boots a SQL Server container, creates the schema from the DDL script,
/// and applies all stored-procedure migrations (029–046). Test classes that share this fixture
/// get an already-initialised database — each test creates its own scoped DbContext so they
/// don't interfere with each other.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Build connection string pointing at the device_management database
        var masterCs = _container.GetConnectionString();
        ConnectionString = masterCs.Replace("master", "device_management");

        // Apply DDL (creates DB + all tables + seed data)
        var ddlPath = FindFile("Docs", "ComputerInventory_DDL.sql");
        var ddlSql = await File.ReadAllTextAsync(ddlPath);
        await ExecuteBatchedSqlAsync(masterCs, ddlSql);

        // Apply stored procedure migrations in order (029–046)
        var migrationsDir = FindDirectory("server", "migrations");
        var spMigrations = Directory.GetFiles(migrationsDir, "*.sql")
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                return int.TryParse(name[..3], out var n) && n >= 29;
            })
            .OrderBy(f => f);

        foreach (var migration in spMigrations)
        {
            var sql = await File.ReadAllTextAsync(migration);
            await ExecuteBatchedSqlAsync(ConnectionString, sql);
        }
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Creates a fresh ApplicationDbContext for use within a single test.
    /// </summary>
    public ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString, sql =>
            {
                sql.EnableRetryOnFailure(3);
                sql.CommandTimeout(30);
            })
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ApplicationDbContext(options);
    }

    /// <summary>
    /// Executes a SQL script that may contain GO separators.
    /// Each batch between GO statements is sent individually.
    /// </summary>
    private static async Task ExecuteBatchedSqlAsync(string connectionString, string script)
    {
        // Split on GO lines (case-insensitive, standalone line)
        var batches = System.Text.RegularExpressions.Regex.Split(
            script,
            @"^\s*GO\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var batch in batches)
        {
            var trimmed = batch.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            await using var command = connection.CreateCommand();
            command.CommandText = trimmed;
            command.CommandTimeout = 60;
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Walks up from the test assembly directory to find the repository root,
    /// then locates the specified file.
    /// </summary>
    private static string FindFile(string folder, string fileName)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, folder, fileName);
            if (File.Exists(candidate)) return candidate;

            // Also check if we're at the solution root (has ComputerInventory.sln)
            if (File.Exists(Path.Combine(dir, "ComputerInventory.sln")))
                return Path.Combine(dir, folder, fileName);

            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException($"Could not find {folder}/{fileName} from {AppContext.BaseDirectory}");
    }

    private static string FindDirectory(string folder, string subFolder)
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, folder, subFolder);
            if (Directory.Exists(candidate)) return candidate;

            if (File.Exists(Path.Combine(dir, "ComputerInventory.sln")))
                return Path.Combine(dir, folder, subFolder);

            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new DirectoryNotFoundException($"Could not find {folder}/{subFolder} from {AppContext.BaseDirectory}");
    }
}

/// <summary>
/// xUnit collection that shares a single SQL Server container across all integration tests.
/// </summary>
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
