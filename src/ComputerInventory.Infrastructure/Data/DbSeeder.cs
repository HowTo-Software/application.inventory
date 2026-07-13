using Microsoft.EntityFrameworkCore;

namespace ComputerInventory.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Admin account is now created through the first-run setup flow (/setup).
        // No default credentials are seeded.
        await Task.CompletedTask;
    }
}
