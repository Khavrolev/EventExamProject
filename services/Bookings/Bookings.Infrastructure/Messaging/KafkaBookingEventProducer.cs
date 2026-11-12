using System.Text.Json;
using Bookings.Application.Abstractions;
using Confluent.Kafka;
using Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bookings.Infrastructure.Messaging;

public sealed class KafkaBookingEventProducer : IBookingEventProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaBookingEventProducer> _logger;

    public KafkaBookingEventProducer(
        IOptions<KafkaOptions> options,
        ILogger<KafkaBookingEventProducer> logger)
    {
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            Acks = Acks.All
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishBookingConfirmedAsync(BookingConfirmed @event, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(@event);
        var message = new Message<string, string>
        {
            Key = @event.EventId.ToString(),
            Value = json
        };

        var deliveryResult = await _producer.ProduceAsync(KafkaTopics.BookingConfirmed, message, cancellationToken);

        _logger.LogInformation(
            "Published BookingConfirmed event for BookingId={BookingId}, EventId={EventId} to topic {Topic} (Partition={Partition}, Offset={Offset})",
            @event.BookingId, @event.EventId, deliveryResult.Topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value);
    }

    public void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
            _producer.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing Kafka producer");
        }
    }
}
