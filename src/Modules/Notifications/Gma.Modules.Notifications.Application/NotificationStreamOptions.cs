namespace Gma.Modules.Notifications.Application;

public sealed class NotificationStreamOptions
{
    public const string SectionName = "Notifications:DurableStreams";
    public const int DefaultBatchSize = 25;
    public const int MaxBatchSize = 100;
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(1);

    public int BatchSize { get; set; } = DefaultBatchSize;
    public TimeSpan PollInterval { get; set; } = DefaultPollInterval;
}
