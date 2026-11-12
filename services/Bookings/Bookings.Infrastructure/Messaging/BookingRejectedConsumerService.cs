using System.Text.Json;
using Bookings.Application.Abstractions;
using Bookings.Domain.Entities;
using Confluent.Kafka;
using Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bookings.Infrastructure.Messaging;

public class BookingRejectedConsumerService(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaOptions> options,
    ILogger<BookingRejectedConsumerService> logger) : BackgroundService
{
    private readonly KafkaOptions _options = options.Value;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);
    }

    private void ConsumeLoop(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = "bookings-rejected-consumer-group",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            EnableAutoOffsetStore = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();

        try
        {
            consumer.Subscribe(KafkaTopics.BookingRejected);
            logger.LogInformation("Subscribed to Kafka topic '{Topic}'", KafkaTopics.BookingRejected);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);

                    if (consumeResult?.Message?.Value is null)
                    {
                        continue;
                    }

                    ProcessMessage(consumeResult.Message.Value);
                    consumer.StoreOffset(consumeResult);
                }
                catch (ConsumeException ex)
                {
                    logger.LogWarning(ex, "Kafka consumption error: {Reason}", ex.Error.Reason);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error processing BookingRejected message");
                }
            }
        }
        finally
        {
            try
            {
                consumer.Close();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error while closing consumer");
            }
        }
    }

    private void ProcessMessage(string messageJson)
    {
        BookingRejected? rejectedEvent;

        try
        {
            rejectedEvent = JsonSerializer.Deserialize<BookingRejected>(messageJson);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize BookingRejected message");
            return;
        }

        if (rejectedEvent == null)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();

        var booking = bookingRepository.GetByIdAsync(rejectedEvent.BookingId).GetAwaiter().GetResult();
        if (booking == null)
        {
            return;
        }

        if (booking.Status != BookingStatus.Rejected && booking.Status != BookingStatus.Cancelled)
        {
            booking.Reject();
            bookingRepository.UpdateAsync(booking).GetAwaiter().GetResult();
            logger.LogInformation("Booking {BookingId} updated to Rejected: {Reason}", booking.Id, rejectedEvent.Reason);
        }
    }
}
