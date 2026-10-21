using EventExamProject.Domain.Entities;
using EventExamProject.Domain.Exceptions;
using EventExamProject.Infrastructure.Repositories;
using EventExamProject.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EventExamProject.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class UserRepositoryTests(DatabaseFixture fixture) : RepositoryTestBase(fixture)
{
    [Fact]
    public async Task AddAsync_ShouldPersistUser_ToDatabase()
    {
        // Arrange
        var repository = new UserRepository(Context);
        var user = User.Create("alice", "HASH", UserRole.Admin);

        // Act
        await repository.AddAsync(user);

        // Assert
        await using var verificationContext = CreateContext();
        var stored = await verificationContext.Users.FindAsync(user.Id);

        stored.Should().NotBeNull();
        stored!.Login.Should().Be("alice");
        stored.PasswordHash.Should().Be("HASH");
        stored.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task AddAsync_ShouldThrow_WhenLoginAlreadyExists()
    {
        // Arrange
        var repository = new UserRepository(Context);
        await repository.AddAsync(User.Create("alice", "HASH"));

        // Act
        var act = () => repository.AddAsync(User.Create("alice", "OTHER"));

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>("the unique index on Login should reject duplicates")
            .WithMessage("*alice*");
    }

    [Fact]
    public async Task AddAsync_ShouldAllowOnlyOneUser_WhenSameLoginIsAddedConcurrently()
    {
        // Arrange: separate contexts, like separate HTTP requests
        var attempts = Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            await using var context = CreateContext();
            var repository = new UserRepository(context);

            try
            {
                await repository.AddAsync(User.Create("alice", "HASH"));
                return true;
            }
            catch (DomainValidationException)
            {
                return false;
            }
        }));

        // Act
        var results = await Task.WhenAll(attempts);

        // Assert
        results.Count(succeeded => succeeded).Should().Be(1);
        await using var verificationContext = CreateContext();
        (await verificationContext.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetByLoginAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        var repository = new UserRepository(Context);
        var user = User.Create("alice", "HASH");
        await repository.AddAsync(user);

        // Act
        var found = await repository.GetByLoginAsync("alice");

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByLoginAsync_ShouldReturnNull_WhenUserDoesNotExist()
    {
        // Arrange
        var repository = new UserRepository(Context);

        // Act
        var found = await repository.GetByLoginAsync("nobody");

        // Assert
        found.Should().BeNull();
    }
}
