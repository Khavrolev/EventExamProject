using EventExamProject.Application.Abstractions;
using EventExamProject.Application.DTOs.User;
using EventExamProject.Application.Services.Interfaces;
using EventExamProject.Domain.Entities;
using EventExamProject.Domain.Exceptions;

namespace EventExamProject.Application.Services;

internal class UserService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator) : IUserService
{
    // Same message for an unknown login and a wrong password, so the API
    // does not reveal which logins exist.
    private const string InvalidCredentialsMessage = "Invalid login or password";

    public async Task RegisterAsync(RegisterUserDto dto)
    {
        var login = dto.Login.Trim();

        if (await userRepository.GetByLoginAsync(login) is not null)
        {
            throw new DomainValidationException($"User with login '{login}' already exists");
        }

        var user = User.Create(login, passwordHasher.Hash(dto.Password), dto.Role);

        await userRepository.AddAsync(user);
    }

    public async Task<TokenDto> LoginAsync(LoginDto dto)
    {
        var user = await userRepository.GetByLoginAsync(dto.Login.Trim());

        if (user is null || !passwordHasher.Verify(dto.Password, user.PasswordHash))
        {
            throw new DomainValidationException(InvalidCredentialsMessage);
        }

        return new TokenDto { Token = jwtTokenGenerator.GenerateToken(user.Id, user.Login, user.Role) };
    }
}
