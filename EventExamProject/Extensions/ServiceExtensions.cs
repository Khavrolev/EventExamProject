using EventExamProject.DataAccess;
using EventExamProject.DataAccess.Interfaces;
using EventExamProject.Services;
using EventExamProject.Services.Interfaces;

namespace EventExamProject.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddControllers();
        
        services.AddSingleton<IEventStore, InMemoryEventStore>();
        services.AddSingleton<IEventService, EventService>();
        
        services.AddSingleton<IBookingStore, InMemoryBookingStore>();
        services.AddSingleton<IBookingService, BookingService>();
        
        services.AddHostedService<BookingProcessingService>();

        return services;
    }
}