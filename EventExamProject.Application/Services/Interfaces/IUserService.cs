using EventExamProject.Application.DTOs.User;

namespace EventExamProject.Application.Services.Interfaces;

public interface IUserService
{
    Task RegisterAsync(RegisterUserDto dto);
    Task<TokenDto> LoginAsync(LoginDto dto);
}
