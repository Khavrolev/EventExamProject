using Microsoft.OpenApi;

namespace EventExamProject.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "EventExamProject API",
                Version = "v1",
                Description = "Сервис для управления мероприятиями и бронированием мест"
            });
        });

        return services;
    }
}