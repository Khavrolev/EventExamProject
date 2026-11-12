using Microsoft.OpenApi;

namespace Users.Presentation.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Users/Auth API",
                Version = "v1",
                Description = "Сервис аутентификации и пользователей"
            });
        });

        return services;
    }
}
