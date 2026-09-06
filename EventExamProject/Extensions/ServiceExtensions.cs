using EventExamProject.DataAccess;
using EventExamProject.Services;
using EventExamProject.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventExamProject.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();

        services.AddHostedService<BookingProcessingService>();

        return services;
    }
}
