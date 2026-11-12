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

    public async Task PublishAsync(string topic, string key, string payload, CancellationToken cancellationToken = default)
    {
        var message = new Message<string, string>
        {
            Key = key,
            Value = payload
        };

        var deliveryResult = await _producer.ProduceAsync(topic, message, cancellationToken);

        _logger.LogInformation(
            "Published message to topic {Topic} (Partition={Partition}, Offset={Offset}, Key={Key})",
            deliveryResult.Topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value, key);
    }

    public Task PublishBookingConfirmedAsync(BookingConfirmed @event, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(@event);
        return PublishAsync(KafkaTopics.BookingConfirmed, @event.EventId.ToString(), json, cancellationToken);
    }

    public Task PublishBookingCancelledAsync(BookingCancelled @event, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(@event);
        return PublishAsync(KafkaTopics.BookingCancelled, @event.EventId.ToString(), json, cancellationToken);
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
