using EventExamProject.Domain.Entities;
using EventExamProject.Infrastructure.Persistence;
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

    protected async Task<User> SeedUserAsync(string? login = null, UserRole role = UserRole.User)
    {
        var user = User.Create(login ?? $"user-{Guid.NewGuid():N}", "HASH", role);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        return user;
    }

    protected AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}
