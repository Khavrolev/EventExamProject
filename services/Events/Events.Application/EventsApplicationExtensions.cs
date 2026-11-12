using Microsoft.Extensions.DependencyInjection;
using Events.Application.Services;
using Events.Application.Services.Interfaces;

namespace Events.Application;

public static class EventsApplicationExtensions
{
    public static IServiceCollection AddEventsApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventService>();
        return services;
    }
}
