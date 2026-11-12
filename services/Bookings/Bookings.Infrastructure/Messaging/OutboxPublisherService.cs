using Bookings.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bookings.Infrastructure.Messaging;

public class OutboxPublisherService(
    IServiceScopeFactory scopeFactory,
    IBookingEventProducer eventProducer,
    ILogger<OutboxPublisherService> logger) : BackgroundService
{
    private const int PollingInterval = 2000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

                var messages = await outboxRepository.GetUnprocessedMessagesAsync(batchSize: 20, stoppingToken);

                foreach (var message in messages)
                {
                    try
                    {
                        await eventProducer.PublishAsync(message.Topic, message.Key, message.Payload, stoppingToken);
                        await outboxRepository.MarkAsProcessedAsync(message.Id, stoppingToken);
                        logger.LogInformation("Dispatched outbox message {MessageId} to topic {Topic}", message.Id, message.Topic);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to dispatch outbox message {MessageId}", message.Id);
                        await outboxRepository.MarkAsFailedAsync(message.Id, ex.Message, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred in OutboxPublisherService");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }
}
