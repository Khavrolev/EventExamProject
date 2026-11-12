namespace Bookings.Infrastructure.Messaging;

public class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";
}
