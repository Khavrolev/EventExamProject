using Bookings.Application.Services;
using Bookings.Application.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Bookings.Application;

public static class BookingsApplicationExtensions
{
    public static IServiceCollection AddBookingsApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IBookingService, BookingService>();
        services.AddHostedService<BookingProcessingService>();
        return services;
    }
}
