namespace Events.Application.Common;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;
    public int EventTtlMinutes { get; set; } = 10;
    public int Top10TtlMinutes { get; set; } = 5;
    public int ConnectTimeoutMs { get; set; } = 2000;
    public int SyncTimeoutMs { get; set; } = 2000;
    public int AsyncTimeoutMs { get; set; } = 2000;
}
