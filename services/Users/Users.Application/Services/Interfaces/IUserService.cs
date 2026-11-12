using Users.Application.DTOs;

namespace Users.Application.Services.Interfaces;

public interface IUserService
{
    Task RegisterAsync(RegisterUserDto dto);
    Task<TokenDto> LoginAsync(LoginDto dto);
}
