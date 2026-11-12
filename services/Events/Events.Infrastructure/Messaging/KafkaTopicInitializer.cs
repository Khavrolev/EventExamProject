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
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var config = new AdminClientConfig
            {
                BootstrapServers = options.Value.BootstrapServers
            };

            using var adminClient = new AdminClientBuilder(config).Build();

            try
            {
                var topicSpecification = new TopicSpecification
                {
                    Name = KafkaTopics.BookingConfirmed,
                    NumPartitions = 3,
                    ReplicationFactor = 1
                };

                await adminClient.CreateTopicsAsync([topicSpecification]);
                logger.LogInformation("Topic '{Topic}' successfully created via admin client", KafkaTopics.BookingConfirmed);
            }
            catch (CreateTopicsException ex)
            {
                var alreadyExists = ex.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists);
                if (alreadyExists)
                {
                    logger.LogInformation("Topic '{Topic}' already exists, skipping creation", KafkaTopics.BookingConfirmed);
                }
                else
                {
                    logger.LogWarning(ex, "Kafka admin client could not create topic '{Topic}'", KafkaTopics.BookingConfirmed);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not initialize Kafka topic '{Topic}' on startup", KafkaTopics.BookingConfirmed);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
