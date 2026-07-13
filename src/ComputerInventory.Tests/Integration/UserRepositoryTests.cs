using ComputerInventory.Core.Entities;
using ComputerInventory.Infrastructure.Data;
using ComputerInventory.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComputerInventory.Tests.Integration;

/// <summary>
/// Integration tests for UserRepository stored procedures.
/// Verifies sp_get_all_users, sp_get_user_by_username, sp_get_user_by_email,
/// sp_check_user_exists, sp_get_users_by_role.
/// </summary>
[Collection("Database")]
public class UserRepositoryTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private ApplicationDbContext _context = default!;
    private UserRepository _repo = default!;

    public UserRepositoryTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
        _repo = new UserRepository(_context, NullLogger<UserRepository>.Instance);
        await TestDataSeeder.SeedAsync(_fixture.CreateDbContext());
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await using var cleanup = _fixture.CreateDbContext();
        await cleanup.Database.ExecuteSqlRawAsync("""
            DELETE FROM computer_histories;
            DELETE FROM maintenance_records;
            DELETE FROM computer_geo_locations;
            DELETE FROM computer_agent_statuses;
            DELETE FROM version_histories;
            DELETE FROM users;
            DELETE FROM computers;
            DELETE FROM operating_systems;
            DELETE FROM computer_models;
            DELETE FROM manufacturers;
            DELETE FROM departments;
            DELETE FROM locations;
            DELETE FROM countries;
            """);
    }

    // ── sp_get_all_users ────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsAllUsers()
    {
        var (items, totalCount) = await _repo.GetAllAsync(page: 1, pageSize: 50);

        // Seeded 3 users + the default admin from DDL
        Assert.True(totalCount >= 3, $"Expected at least 3 users, got {totalCount}");
        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task GetAllAsync_PaginationWorks()
    {
        var (page1, total) = await _repo.GetAllAsync(page: 1, pageSize: 2);

        Assert.Equal(2, page1.Count());
        Assert.True(total >= 3);
    }

    [Fact]
    public async Task GetAllAsync_SearchByUsername()
    {
        var (items, totalCount) = await _repo.GetAllAsync(page: 1, pageSize: 50, searchTerm: "admin");

        Assert.True(totalCount >= 1);
        Assert.Contains(items, u => u.Username.Contains("admin", StringComparison.OrdinalIgnoreCase));
    }

    // ── sp_get_user_by_username ─────────────────────────────────────────

    [Fact]
    public async Task GetByUsernameAsync_FindsExisting()
    {
        var user = await _repo.GetByUsernameAsync("admin");

        Assert.NotNull(user);
        Assert.Equal("admin", user.Username);
    }

    [Fact]
    public async Task GetByUsernameAsync_ReturnsNull_ForMissing()
    {
        var result = await _repo.GetByUsernameAsync("nonexistentuser");

        Assert.Null(result);
    }

    // ── sp_get_user_by_email ────────────────────────────────────────────

    [Fact]
    public async Task GetByEmailAsync_FindsExisting()
    {
        var user = await _repo.GetByEmailAsync("admin@test.com");

        Assert.NotNull(user);
        Assert.Equal("admin@test.com", user.Email);
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsNull_ForMissing()
    {
        var result = await _repo.GetByEmailAsync("nobody@nowhere.com");

        Assert.Null(result);
    }

    // ── sp_check_user_exists ────────────────────────────────────────────

    [Fact]
    public async Task UserExistsAsync_DetectsExistingUsername()
    {
        var (usernameExists, emailExists) = await _repo.UserExistsAsync("admin", "unique@email.com");

        Assert.True(usernameExists);
        Assert.False(emailExists);
    }

    [Fact]
    public async Task UserExistsAsync_DetectsExistingEmail()
    {
        var (usernameExists, emailExists) = await _repo.UserExistsAsync("uniqueuser", "admin@test.com");

        Assert.False(usernameExists);
        Assert.True(emailExists);
    }

    [Fact]
    public async Task UserExistsAsync_ReturnsFalse_ForNoMatch()
    {
        var (usernameExists, emailExists) = await _repo.UserExistsAsync("uniqueuser", "unique@email.com");

        Assert.False(usernameExists);
        Assert.False(emailExists);
    }

    // ── sp_get_users_by_role ────────────────────────────────────────────

    [Fact]
    public async Task GetByRoleAsync_ReturnsUsersWithRole()
    {
        var admins = await _repo.GetByRoleAsync(UserRole.Admin);

        Assert.NotEmpty(admins);
        Assert.All(admins, u => Assert.Equal(2, u.RoleId)); // Admin = 2
    }
}
