using EventExamProject.DataAccess;
using EventExamProject.DataAccess.Interfaces;
using EventExamProject.DataAccess.Repositories;
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

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();

        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();

        services.AddHostedService<BookingProcessingService>();

        return services;
    }
}
