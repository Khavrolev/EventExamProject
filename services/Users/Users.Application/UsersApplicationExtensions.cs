using Microsoft.Extensions.DependencyInjection;
using Users.Application.Services;
using Users.Application.Services.Interfaces;

namespace Users.Application;

public static class UsersApplicationExtensions
{
    public static IServiceCollection AddUsersApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
