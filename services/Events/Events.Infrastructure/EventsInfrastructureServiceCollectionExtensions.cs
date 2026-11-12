using Confluent.Kafka;
using Events.Application.Abstractions;
using Events.Infrastructure.Messaging;
using Events.Infrastructure.Persistence;
using Events.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Events.Infrastructure;

public static class EventsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddEventsInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<EventsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.Configure<KafkaConsumerOptions>(configuration.GetSection(KafkaConsumerOptions.SectionName));

        services.AddSingleton<IProducer<string, string>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<KafkaConsumerOptions>>().Value;
            var config = new ProducerConfig
            {
                BootstrapServers = options.BootstrapServers,
                Acks = Acks.All
            };
            return new ProducerBuilder<string, string>(config).Build();
        });

        services.AddHostedService<KafkaTopicInitializer>();
        services.AddHostedService<BookingConfirmedConsumerService>();

        services.AddScoped<IEventRepository, EventRepository>();

        return services;
    }
}
