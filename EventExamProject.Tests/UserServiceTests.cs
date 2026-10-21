using EventExamProject.Application.Abstractions;
using EventExamProject.Application.DTOs.User;
using EventExamProject.Application.Services;
using EventExamProject.Application.Services.Interfaces;
using EventExamProject.Domain.Entities;
using EventExamProject.Domain.Exceptions;
using EventExamProject.Infrastructure.Persistence;
using EventExamProject.Infrastructure.Repositories;
using EventExamProject.Infrastructure.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EventExamProject.Tests;

public class UserServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IUserService _userService;
    private readonly AppDbContext _context;

    public UserServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, Sha256PasswordHasher>();
        services.AddSingleton(Options.Create(new JwtOptions
        {
            Secret = "unit-test-secret-which-is-long-enough-0123456789",
            Issuer = "tests",
            Audience = "tests"
        }));
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IUserService, UserService>();

        _serviceProvider = services.BuildServiceProvider();
        _userService = _serviceProvider.GetRequiredService<IUserService>();
        _context = _serviceProvider.GetRequiredService<AppDbContext>();
    }

    public void Dispose() => _serviceProvider.Dispose();

    [Fact]
    public async Task Register_ShouldStoreHashedPassword_NotPlainText()
    {
        await _userService.RegisterAsync(new RegisterUserDto { Login = "alice", Password = "secret123" });

        var stored = await _context.Users.SingleAsync();
        stored.PasswordHash.Should().NotBe("secret123");
        stored.PasswordHash.Should().NotContain("secret123");
    }

    [Fact]
    public async Task Register_ShouldUseUserRole_ByDefault()
    {
        await _userService.RegisterAsync(new RegisterUserDto { Login = "alice", Password = "secret123" });

        (await _context.Users.SingleAsync()).Role.Should().Be(UserRole.User);
    }

    [Fact]
    public async Task Register_ShouldStoreRequestedRole_WhenAdminIsPassed()
    {
        await _userService.RegisterAsync(
            new RegisterUserDto { Login = "boss", Password = "secret123", Role = UserRole.Admin });

        (await _context.Users.SingleAsync()).Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task Register_ShouldThrowDomainValidationException_WhenLoginAlreadyExists()
    {
        await _userService.RegisterAsync(new RegisterUserDto { Login = "alice", Password = "secret123" });

        await FluentActions.Awaiting(() =>
                _userService.RegisterAsync(new RegisterUserDto { Login = "alice", Password = "other-pass" }))
            .Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task Login_ShouldReturnToken_WhenCredentialsAreCorrect()
    {
        await _userService.RegisterAsync(new RegisterUserDto { Login = "alice", Password = "secret123" });

        var result = await _userService.LoginAsync(new LoginDto { Login = "alice", Password = "secret123" });

        result.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_ShouldThrowDomainValidationException_WhenPasswordIsWrong()
    {
        await _userService.RegisterAsync(new RegisterUserDto { Login = "alice", Password = "secret123" });

        await FluentActions.Awaiting(() =>
                _userService.LoginAsync(new LoginDto { Login = "alice", Password = "wrong-pass" }))
            .Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task Login_ShouldReturnSameError_ForUnknownLoginAndWrongPassword()
    {
        await _userService.RegisterAsync(new RegisterUserDto { Login = "alice", Password = "secret123" });

        var wrongPassword = await Record.ExceptionAsync(() =>
            _userService.LoginAsync(new LoginDto { Login = "alice", Password = "wrong-pass" }));
        var unknownLogin = await Record.ExceptionAsync(() =>
            _userService.LoginAsync(new LoginDto { Login = "nobody", Password = "secret123" }));

        wrongPassword.Should().BeOfType<DomainValidationException>();
        unknownLogin.Should().BeOfType<DomainValidationException>();
        unknownLogin!.Message.Should().Be(wrongPassword!.Message);
    }
}
