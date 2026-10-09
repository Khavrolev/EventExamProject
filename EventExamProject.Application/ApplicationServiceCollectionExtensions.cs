using EventExamProject.Application.Services;
using EventExamProject.Application.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace EventExamProject.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();

        services.AddHostedService<BookingProcessingService>();

        return services;
    }
}
