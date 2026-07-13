using System.Data;
using ComputerInventory.Contracts;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Infrastructure.Data;
using ComputerInventory.Infrastructure.Sorting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace ComputerInventory.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserRepository> _logger;

    // Compiled queries removed — GetByIdAsync now uses sp_get_user_by_id





    public UserRepository(ApplicationDbContext context, ILogger<UserRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public IQueryable<ApplicationUser> Query => _context.Users.AsNoTracking();

    public async Task<(IEnumerable<ApplicationUser> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? searchTerm = null, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default)
    {
        var dir = string.IsNullOrWhiteSpace(sortDirection) ? "ASC"
            : sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC"
            : "ASC";

        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "EXEC [sp_get_all_users] @search_term, @sort_by, @sort_direction, @page, @page_size";
            command.Parameters.Add(new SqlParameter("@search_term", SqlDbType.NVarChar, 100) { Value = (object?)searchTerm ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@sort_by", SqlDbType.NVarChar, 50) { Value = (object?)sortBy?.ToLowerInvariant() ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@sort_direction", SqlDbType.NVarChar, 4) { Value = dir });
            command.Parameters.Add(new SqlParameter("@page", SqlDbType.Int) { Value = page });
            command.Parameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = pageSize });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // First result set: total count
            int totalCount = 0;
            if (await reader.ReadAsync(cancellationToken))
                totalCount = reader.GetInt32(0);

            // Second result set: paged user records
            var items = new List<ApplicationUser>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var user = new ApplicationUser
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        Username = reader.GetString(reader.GetOrdinal("username")),
                        Email = reader.GetString(reader.GetOrdinal("email")),
                        FirstName = reader.GetString(reader.GetOrdinal("first_name")),
                        LastName = reader.GetString(reader.GetOrdinal("last_name")),
                        DepartmentId = reader.IsDBNull(reader.GetOrdinal("department_id")) ? null : reader.GetInt32(reader.GetOrdinal("department_id")),
                        Phone = reader.IsDBNull(reader.GetOrdinal("phone")) ? null : reader.GetString(reader.GetOrdinal("phone")),
                        RoleId = reader.GetInt32(reader.GetOrdinal("role_id")),
                        IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                        IsEmailVerified = reader.GetBoolean(reader.GetOrdinal("is_email_verified")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                        UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime(reader.GetOrdinal("updated_at")),
                        LastLoginAt = reader.IsDBNull(reader.GetOrdinal("last_login_at")) ? null : reader.GetDateTime(reader.GetOrdinal("last_login_at")),
                    };

                    // Hydrate navigation properties from denormalized SP columns
                    if (user.DepartmentId.HasValue && !reader.IsDBNull(reader.GetOrdinal("_department_name")))
                    {
                        user.DepartmentEntity = new Department
                        {
                            Id = user.DepartmentId.Value,
                            Name = reader.GetString(reader.GetOrdinal("_department_name")),
                        };
                    }

                    user.RoleLookup = new UserRoleLookup
                    {
                        Id = user.RoleId,
                        Name = reader.GetString(reader.GetOrdinal("_role_name")),
                    };

                    items.Add(user);
                }
            }

            return (items, totalCount);
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    public async Task<ApplicationUser?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "EXEC [sp_get_user_by_id] @id";
            command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return MapUserFromReader(reader);
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    public async Task<ApplicationUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "EXEC [sp_get_user_by_username] @username";
            command.Parameters.Add(new SqlParameter("@username", SqlDbType.NVarChar, 50) { Value = username });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                return null;

            var user = new ApplicationUser
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                Username = reader.GetString(reader.GetOrdinal("username")),
                Email = reader.GetString(reader.GetOrdinal("email")),
                PasswordHash = reader.GetString(reader.GetOrdinal("password_hash")),
                FirstName = reader.GetString(reader.GetOrdinal("first_name")),
                LastName = reader.GetString(reader.GetOrdinal("last_name")),
                DepartmentId = reader.IsDBNull(reader.GetOrdinal("department_id")) ? null : reader.GetInt32(reader.GetOrdinal("department_id")),
                Phone = reader.IsDBNull(reader.GetOrdinal("phone")) ? null : reader.GetString(reader.GetOrdinal("phone")),
                RoleId = reader.GetInt32(reader.GetOrdinal("role_id")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                IsEmailVerified = reader.GetBoolean(reader.GetOrdinal("is_email_verified")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime(reader.GetOrdinal("updated_at")),
                LastLoginAt = reader.IsDBNull(reader.GetOrdinal("last_login_at")) ? null : reader.GetDateTime(reader.GetOrdinal("last_login_at")),
            };

            if (user.DepartmentId.HasValue && !reader.IsDBNull(reader.GetOrdinal("_department_name")))
            {
                user.DepartmentEntity = new Department
                {
                    Id = user.DepartmentId.Value,
                    Name = reader.GetString(reader.GetOrdinal("_department_name")),
                };
            }

            user.RoleLookup = new UserRoleLookup
            {
                Id = user.RoleId,
                Name = reader.GetString(reader.GetOrdinal("_role_name")),
            };

            return user;
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    public async Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "EXEC [sp_get_user_by_email] @email";
            command.Parameters.Add(new SqlParameter("@email", SqlDbType.NVarChar, 256) { Value = email });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                return null;

            var user = new ApplicationUser
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                Username = reader.GetString(reader.GetOrdinal("username")),
                Email = reader.GetString(reader.GetOrdinal("email")),
                PasswordHash = reader.GetString(reader.GetOrdinal("password_hash")),
                FirstName = reader.GetString(reader.GetOrdinal("first_name")),
                LastName = reader.GetString(reader.GetOrdinal("last_name")),
                DepartmentId = reader.IsDBNull(reader.GetOrdinal("department_id")) ? null : reader.GetInt32(reader.GetOrdinal("department_id")),
                Phone = reader.IsDBNull(reader.GetOrdinal("phone")) ? null : reader.GetString(reader.GetOrdinal("phone")),
                RoleId = reader.GetInt32(reader.GetOrdinal("role_id")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                IsEmailVerified = reader.GetBoolean(reader.GetOrdinal("is_email_verified")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime(reader.GetOrdinal("updated_at")),
                LastLoginAt = reader.IsDBNull(reader.GetOrdinal("last_login_at")) ? null : reader.GetDateTime(reader.GetOrdinal("last_login_at")),
            };

            if (user.DepartmentId.HasValue && !reader.IsDBNull(reader.GetOrdinal("_department_name")))
            {
                user.DepartmentEntity = new Department
                {
                    Id = user.DepartmentId.Value,
                    Name = reader.GetString(reader.GetOrdinal("_department_name")),
                };
            }

            user.RoleLookup = new UserRoleLookup
            {
                Id = user.RoleId,
                Name = reader.GetString(reader.GetOrdinal("_role_name")),
            };

            return user;
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    public async Task<IEnumerable<ApplicationUser>> GetByRoleAsync(UserRole role, CancellationToken cancellationToken = default)
    {
        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "EXEC [sp_get_users_by_role] @role_id";
            command.Parameters.Add(new SqlParameter("@role_id", SqlDbType.Int) { Value = (int)role });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var items = new List<ApplicationUser>();
            while (await reader.ReadAsync(cancellationToken))
                items.Add(MapUserFromReader(reader));
            return items;
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    public async Task<ApplicationUser> AddAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        try
        {
            user.CreatedAt = DateTime.UtcNow;
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add user {Username}", user.Username);
            throw;
        }
    }

    public async Task UpdateAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        try
        {
            user.UpdatedAt = DateTime.UtcNow;
            _context.Entry(user).State = EntityState.Modified;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user {UserId}", user.Id);
            throw;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _context.Users.FindAsync(new object[] { id }, cancellationToken);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user {UserId}", id);
            throw;
        }
    }

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        var (usernameExists, _) = await UserExistsAsync(username, null!, cancellationToken);
        return usernameExists;
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var (_, emailExists) = await UserExistsAsync(null!, email, cancellationToken);
        return emailExists;
    }

    public async Task<(bool UsernameExists, bool EmailExists)> UserExistsAsync(string username, string email, CancellationToken cancellationToken = default)
    {
        var usernameExistsParam = SpParam.Output("@username_exists", SqlDbType.Bit);
        var emailExistsParam = SpParam.Output("@email_exists", SqlDbType.Bit);

        await _context.ExecuteSpNonQueryAsync(
            "sp_check_user_exists", cancellationToken,
            SpParam.Input("@username", string.IsNullOrEmpty(username) ? null : username),
            SpParam.Input("@email", string.IsNullOrEmpty(email) ? null : email),
            usernameExistsParam,
            emailExistsParam);

        return (
            usernameExistsParam.Value is true,
            emailExistsParam.Value is true);
    }

    // ── SP result-set mapper ────────────────────────────────────────────

    public async Task<ApplicationUser?> GetByVerificationTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.EmailVerificationToken == tokenHash, cancellationToken);
    }


    private static ApplicationUser MapUserFromReader(System.Data.Common.DbDataReader reader)
    {
        var user = new ApplicationUser
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            Username = reader.GetString(reader.GetOrdinal("username")),
            Email = reader.GetString(reader.GetOrdinal("email")),
            FirstName = reader.GetString(reader.GetOrdinal("first_name")),
            LastName = reader.GetString(reader.GetOrdinal("last_name")),
            DepartmentId = reader.IsDBNull(reader.GetOrdinal("department_id")) ? null : reader.GetInt32(reader.GetOrdinal("department_id")),
            Phone = reader.IsDBNull(reader.GetOrdinal("phone")) ? null : reader.GetString(reader.GetOrdinal("phone")),
            RoleId = reader.GetInt32(reader.GetOrdinal("role_id")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
            IsEmailVerified = reader.GetBoolean(reader.GetOrdinal("is_email_verified")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime(reader.GetOrdinal("updated_at")),
            LastLoginAt = reader.IsDBNull(reader.GetOrdinal("last_login_at")) ? null : reader.GetDateTime(reader.GetOrdinal("last_login_at")),
        };

        if (user.DepartmentId.HasValue && !reader.IsDBNull(reader.GetOrdinal("_department_name")))
        {
            user.DepartmentEntity = new Department
            {
                Id = user.DepartmentId.Value,
                Name = reader.GetString(reader.GetOrdinal("_department_name")),
            };
        }

        user.RoleLookup = new UserRoleLookup
        {
            Id = user.RoleId,
            Name = reader.GetString(reader.GetOrdinal("_role_name")),
        };

        return user;
    }
}
