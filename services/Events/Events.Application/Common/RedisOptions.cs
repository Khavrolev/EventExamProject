namespace Events.Application.Common;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "localhost:6379";
    public int EventTtlMinutes { get; set; } = 10;
    public int Top10TtlMinutes { get; set; } = 5;
}
