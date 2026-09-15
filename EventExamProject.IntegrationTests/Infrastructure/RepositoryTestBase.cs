using EventExamProject.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace EventExamProject.IntegrationTests.Infrastructure;

[Collection(DatabaseCollection.Name)]
public abstract class RepositoryTestBase(DatabaseFixture fixture) : IAsyncLifetime
{
    protected AppDbContext Context { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Context = CreateContext();

        await Context.Database.EnsureDeletedAsync();
        await Context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Context.DisposeAsync();
    }

    protected AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}
