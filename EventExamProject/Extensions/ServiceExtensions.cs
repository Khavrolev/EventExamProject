using EventExamProject.Services;
using EventExamProject.Services.Interfaces;

namespace EventExamProject.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddSingleton<IEventService, EventService>();

        return services;
    }
}