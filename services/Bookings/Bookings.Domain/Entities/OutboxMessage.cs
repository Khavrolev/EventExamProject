namespace Bookings.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public required string Topic { get; set; }
    public required string Key { get; set; }
    public required string Payload { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }

    public static OutboxMessage Create(string topic, string key, string payload) => new()
    {
        Id = Guid.NewGuid(),
        Topic = topic,
        Key = key,
        Payload = payload,
        CreatedAt = DateTime.UtcNow
    };
}
