using EventExamProject.Domain.Entities;
using EventExamProject.Domain.Exceptions;
using FluentAssertions;

namespace EventExamProject.Tests;

public class UserTests
{
    [Fact]
    public void Create_ShouldInitializeUser_WithGivenValues()
    {
        var user = User.Create("alice", "HASH", UserRole.Admin);

        user.Id.Should().NotBe(Guid.Empty);
        user.Login.Should().Be("alice");
        user.PasswordHash.Should().Be("HASH");
        user.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public void Create_ShouldUseUserRole_ByDefault()
    {
        User.Create("alice", "HASH").Role.Should().Be(UserRole.User);
    }

    [Fact]
    public void Create_ShouldTrimLogin()
    {
        User.Create("  alice  ", "HASH").Login.Should().Be("alice");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrowDomainValidationException_WhenLoginIsEmpty(string login)
    {
        var act = () => User.Create(login, "HASH");

        act.Should().Throw<DomainValidationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrowDomainValidationException_WhenPasswordHashIsEmpty(string hash)
    {
        var act = () => User.Create("alice", hash);

        act.Should().Throw<DomainValidationException>();
    }
}
