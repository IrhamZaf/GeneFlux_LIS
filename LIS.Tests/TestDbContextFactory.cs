using LIS.Data;
using Microsoft.EntityFrameworkCore;

namespace LIS.Tests;

internal sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
{
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public TestDbContextFactory(string databaseName)
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    public ApplicationDbContext CreateDbContext() => new(_options);
}
