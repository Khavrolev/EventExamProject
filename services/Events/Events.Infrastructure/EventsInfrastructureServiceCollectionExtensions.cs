using Confluent.Kafka;
using Events.Application.Abstractions;
using Events.Application.Common;
using Events.Infrastructure.Caching;
using Events.Infrastructure.Messaging;
using Events.Infrastructure.Persistence;
using Events.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

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
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));

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

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var connectionString = configuration.GetSection("Redis")["ConnectionString"]
                ?? configuration.GetConnectionString("Redis")
                ?? "localhost:6379";

            var configurationOptions = ConfigurationOptions.Parse(connectionString);
            configurationOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(configurationOptions);
        });

        services.AddSingleton<ICacheService, RedisCacheService>();

        services.AddHostedService<KafkaTopicInitializer>();
        services.AddHostedService<BookingConfirmedConsumerService>();

        services.AddScoped<IEventRepository, EventRepository>();

        return services;
    }
}
