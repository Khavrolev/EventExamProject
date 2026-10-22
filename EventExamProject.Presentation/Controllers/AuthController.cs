using EventExamProject.Application.DTOs.User;
using EventExamProject.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventExamProject.Presentation.Controllers;

[ApiController]
[AllowAnonymous]
[Route("auth")]
public class AuthController(IUserService userService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterUserDto dto)
    {
        await userService.RegisterAsync(dto);

        return NoContent();
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TokenDto>> Login(LoginDto dto)
    {
        var token = await userService.LoginAsync(dto);

        return Ok(token);
    }
}
