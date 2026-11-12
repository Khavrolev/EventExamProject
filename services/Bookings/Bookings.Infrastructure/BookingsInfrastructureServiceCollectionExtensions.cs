using Bookings.Application.Abstractions;
using Bookings.Infrastructure.Messaging;
using Bookings.Infrastructure.Persistence;
using Bookings.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookings.Infrastructure;

public static class BookingsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBookingsInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<BookingsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddSingleton<IBookingEventProducer, KafkaBookingEventProducer>();

        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        services.AddHostedService<OutboxPublisherService>();
        services.AddHostedService<BookingRejectedConsumerService>();

        return services;
    }
}
