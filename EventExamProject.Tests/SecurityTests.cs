using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EventExamProject.Domain.Entities;
using EventExamProject.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EventExamProject.Tests;

public class SecurityTests
{
    private readonly Sha256PasswordHasher _hasher = new();

    private static JwtTokenGenerator CreateGenerator(int expirationMinutes = 60) =>
        new(Options.Create(new JwtOptions
        {
            Secret = "unit-test-secret-which-is-long-enough-0123456789",
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpirationMinutes = expirationMinutes
        }));

    [Fact]
    public void Hash_ShouldBeDeterministic_AndDifferFromPassword()
    {
        var first = _hasher.Hash("secret123");
        var second = _hasher.Hash("secret123");

        first.Should().Be(second);
        first.Should().NotBe("secret123");
        first.Should().HaveLength(64);
    }

    [Fact]
    public void Verify_ShouldReturnTrue_ForMatchingPassword()
    {
        _hasher.Verify("secret123", _hasher.Hash("secret123")).Should().BeTrue();
    }

    [Fact]
    public void Verify_ShouldReturnFalse_ForDifferentPassword()
    {
        _hasher.Verify("other", _hasher.Hash("secret123")).Should().BeFalse();
    }

    [Fact]
    public void GenerateToken_ShouldContainUserIdLoginRoleIssuerAndAudience()
    {
        var userId = Guid.NewGuid();

        var token = new JwtSecurityTokenHandler()
            .ReadJwtToken(CreateGenerator().GenerateToken(userId, "alice", UserRole.Admin));

        token.Issuer.Should().Be("test-issuer");
        token.Audiences.Should().Contain("test-audience");
        token.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == userId.ToString());
        token.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.UniqueName && c.Value == "alice");
    }

    [Fact]
    public void GenerateToken_ShouldExpireAfterConfiguredLifetime()
    {
        var token = new JwtSecurityTokenHandler()
            .ReadJwtToken(CreateGenerator(expirationMinutes: 5).GenerateToken(Guid.NewGuid(), "alice", UserRole.User));

        token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(30));
    }
}
