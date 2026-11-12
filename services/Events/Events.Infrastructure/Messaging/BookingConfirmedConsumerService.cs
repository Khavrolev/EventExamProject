using System.Text.Json;
using Confluent.Kafka;
using Contracts;
using Events.Domain.Entities;
using Events.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Events.Infrastructure.Messaging;

public class BookingConfirmedConsumerService(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaConsumerOptions> options,
    IProducer<string, string> producer,
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
            consumer.Subscribe([KafkaTopics.BookingConfirmed, KafkaTopics.BookingCancelled]);
            logger.LogInformation("Subscribed to Kafka topics '{Confirmed}' and '{Cancelled}' with GroupId '{GroupId}'",
                KafkaTopics.BookingConfirmed, KafkaTopics.BookingCancelled, _options.ConsumerGroup);

            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? consumeResult = null;
                try
                {
                    consumeResult = consumer.Consume(stoppingToken);

                    if (consumeResult?.Message?.Value is null)
                    {
                        continue;
                    }

                    if (consumeResult.Topic == KafkaTopics.BookingConfirmed)
                    {
                        ProcessBookingConfirmed(consumeResult.Message.Value);
                    }
                    else if (consumeResult.Topic == KafkaTopics.BookingCancelled)
                    {
                        ProcessBookingCancelled(consumeResult.Message.Value);
                    }

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
                    logger.LogError(ex, "Unexpected error processing message, seeking to retry");
                    if (consumeResult != null)
                    {
                        try
                        {
                            consumer.Seek(consumeResult.TopicPartitionOffset);
                            Thread.Sleep(2000);
                        }
                        catch (Exception seekEx)
                        {
                            logger.LogError(seekEx, "Failed to seek consumer");
                        }
                    }
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

    private void ProcessBookingConfirmed(string messageJson)
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
            logger.LogWarning("Event with Id {EventId} not found for Booking {BookingId}. Rejecting booking.",
                bookingEvent.EventId, bookingEvent.BookingId);

            PublishBookingRejected(bookingEvent.BookingId, bookingEvent.EventId, "Event not found");

            context.ProcessedBookings.Add(new ProcessedBooking
            {
                BookingId = bookingEvent.BookingId,
                ProcessedAt = DateTime.UtcNow
            });
            context.SaveChanges();
            return;
        }

        var seatsToReserve = bookingEvent.SeatsCount > 0 ? bookingEvent.SeatsCount : 1;
        if (!eventEntity.TryReserveSeats(seatsToReserve))
        {
            logger.LogWarning("Not enough available seats ({Available}/{Total}) for event {EventId} (BookingId: {BookingId}). Rejecting booking.",
                eventEntity.AvailableSeats, eventEntity.TotalSeats, eventEntity.Id, bookingEvent.BookingId);

            PublishBookingRejected(bookingEvent.BookingId, bookingEvent.EventId, "No available seats");

            context.ProcessedBookings.Add(new ProcessedBooking
            {
                BookingId = bookingEvent.BookingId,
                ProcessedAt = DateTime.UtcNow
            });
            context.SaveChanges();
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

    private void ProcessBookingCancelled(string messageJson)
    {
        BookingCancelled? cancelledEvent;

        try
        {
            cancelledEvent = JsonSerializer.Deserialize<BookingCancelled>(messageJson);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize BookingCancelled message: {MessageJson}", messageJson);
            return;
        }

        if (cancelledEvent == null)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

        var processed = context.ProcessedBookings.Find(cancelledEvent.BookingId);
        if (processed == null)
        {
            logger.LogInformation("Booking {BookingId} was never processed, skipping cancellation", cancelledEvent.BookingId);
            return;
        }

        var eventEntity = context.Events.Find(cancelledEvent.EventId);
        if (eventEntity != null)
        {
            var seatsToRelease = cancelledEvent.SeatsCount > 0 ? cancelledEvent.SeatsCount : 1;
            eventEntity.ReleaseSeats(seatsToRelease);
            context.ProcessedBookings.Remove(processed);
            context.SaveChanges();

            logger.LogInformation(
                "Released {Seats} seat(s) for event {EventId} after cancellation of booking {BookingId}. Remaining: {RemainingSeats}",
                seatsToRelease, eventEntity.Id, cancelledEvent.BookingId, eventEntity.AvailableSeats);
        }
    }

    private void PublishBookingRejected(Guid bookingId, Guid eventId, string reason)
    {
        try
        {
            var rejected = new BookingRejected(bookingId, eventId, reason, DateTime.UtcNow);
            var json = JsonSerializer.Serialize(rejected);
            producer.Produce(KafkaTopics.BookingRejected, new Message<string, string>
            {
                Key = eventId.ToString(),
                Value = json
            });
            logger.LogInformation("Published BookingRejected for booking {BookingId}: {Reason}", bookingId, reason);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish BookingRejected for booking {BookingId}", bookingId);
        }
    }
}
