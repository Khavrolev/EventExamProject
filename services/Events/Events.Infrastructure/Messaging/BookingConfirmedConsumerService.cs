using System.Text.Json;
using Confluent.Kafka;
using Contracts;
using Events.Domain.Entities;
using Events.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Events.Infrastructure.Messaging;

public class BookingConfirmedConsumerService(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaConsumerOptions> options,
    ILogger<BookingConfirmedConsumerService> logger) : BackgroundService
{
    private readonly KafkaConsumerOptions _options = options.Value;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);
    }

    private void ConsumeLoop(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            EnableAutoOffsetStore = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();

        try
        {
            consumer.Subscribe(KafkaTopics.BookingConfirmed);
            logger.LogInformation("Subscribed to Kafka topic '{Topic}' with GroupId '{GroupId}'",
                KafkaTopics.BookingConfirmed, _options.ConsumerGroup);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);

                    if (consumeResult?.Message?.Value is null)
                    {
                        continue;
                    }

                    ProcessMessage(consumeResult.Message.Value, stoppingToken);

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
                    logger.LogError(ex, "Unexpected error processing message from topic '{Topic}'", KafkaTopics.BookingConfirmed);
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
                logger.LogWarning(ex, "Error while closing Kafka consumer");
            }
        }
    }

    private void ProcessMessage(string messageJson, CancellationToken cancellationToken)
    {
        BookingConfirmed? bookingEvent;

        try
        {
            bookingEvent = JsonSerializer.Deserialize<BookingConfirmed>(messageJson);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize BookingConfirmed message: {MessageJson}", messageJson);
            return;
        }

        if (bookingEvent == null)
        {
            logger.LogWarning("Skipping null BookingConfirmed payload");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var alreadyProcessed = context.ProcessedBookings.Any(p => p.BookingId == bookingEvent.BookingId);
        if (alreadyProcessed)
        {
            logger.LogInformation("Booking {BookingId} has already been processed for event {EventId}. Skipping duplicate message.",
                bookingEvent.BookingId, bookingEvent.EventId);
            return;
        }

        var eventEntity = context.Events.Find(bookingEvent.EventId);
        if (eventEntity == null)
        {
            logger.LogWarning("Event with Id {EventId} not found for Booking {BookingId}. Skipping message.",
                bookingEvent.EventId, bookingEvent.BookingId);
            return;
        }

        var seatsToReserve = bookingEvent.SeatsCount > 0 ? bookingEvent.SeatsCount : 1;
        if (!eventEntity.TryReserveSeats(seatsToReserve))
        {
            logger.LogWarning(
                "Not enough available seats ({Available}/{Total}) for event {EventId} (BookingId: {BookingId}). Skipping.",
                eventEntity.AvailableSeats, eventEntity.TotalSeats, eventEntity.Id, bookingEvent.BookingId);
            return;
        }

        context.ProcessedBookings.Add(new ProcessedBooking
        {
            BookingId = bookingEvent.BookingId,
            ProcessedAt = DateTime.UtcNow
        });

        context.SaveChanges();

        logger.LogInformation(
            "Reduced {Seats} available seat(s) for event {EventId} ({Title}) after confirming booking {BookingId}. Remaining: {RemainingSeats}",
            seatsToReserve, eventEntity.Id, eventEntity.Title, bookingEvent.BookingId, eventEntity.AvailableSeats);
    }
}
