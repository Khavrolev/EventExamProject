using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Events.Infrastructure.Messaging;

public class KafkaTopicInitializer(
    IOptions<KafkaConsumerOptions> options,
    ILogger<KafkaTopicInitializer> logger) : IHostedService
{
    private static readonly string[] Topics =
    [
        KafkaTopics.BookingConfirmed,
        KafkaTopics.BookingCancelled,
        KafkaTopics.BookingRejected
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var config = new AdminClientConfig
            {
                BootstrapServers = options.Value.BootstrapServers
            };

            using var adminClient = new AdminClientBuilder(config).Build();

            foreach (var topicName in Topics)
            {
                try
                {
                    var topicSpecification = new TopicSpecification
                    {
                        Name = topicName,
                        NumPartitions = 3,
                        ReplicationFactor = 1
                    };

                    await adminClient.CreateTopicsAsync([topicSpecification]);
                    logger.LogInformation("Topic '{Topic}' successfully created via admin client", topicName);
                }
                catch (CreateTopicsException ex)
                {
                    var alreadyExists = ex.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists);
                    if (alreadyExists)
                    {
                        logger.LogInformation("Topic '{Topic}' already exists, skipping creation", topicName);
                    }
                    else
                    {
                        logger.LogWarning(ex, "Kafka admin client could not create topic '{Topic}'", topicName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not initialize Kafka topics on startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
