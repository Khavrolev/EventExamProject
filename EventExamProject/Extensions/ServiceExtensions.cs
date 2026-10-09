using EventExamProject.Application;
using EventExamProject.DataAccess;
using EventExamProject.Application.Abstractions;
using EventExamProject.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventExamProject.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();

        services.AddApplicationServices();

        return services;
    }
}
