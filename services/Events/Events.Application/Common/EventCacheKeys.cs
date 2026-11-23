namespace Events.Application.Common;

public static class EventCacheKeys
{
    public const string Top10 = "events:top10";

    public static string Top(int count = 10) => $"events:top{count}";

    public static string Event(Guid id) => $"event:{id}";
}
