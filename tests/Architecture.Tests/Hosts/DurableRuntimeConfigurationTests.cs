namespace Architecture.Tests;

using System.Text.Json;
using Xunit;

[Trait("Category", "Architecture")]
public sealed class DurableRuntimeConfigurationTests
{
    private static readonly string[] MessagingHosts =
    [
        "Host.Api",
        "Host.AdminApi",
        "Host.AdminCli",
        "Host.Worker",
    ];

    [Fact]
    public void Messaging_hosts_expose_bounded_replay_safe_runtime_defaults()
    {
        string repositoryRoot = FindRepositoryRoot();

        foreach (string host in MessagingHosts)
        {
            using JsonDocument document = ReadAppSettings(repositoryRoot, host);
            JsonElement root = document.RootElement;
            JsonElement cleanup = root.GetProperty("MessageJournalCleanup");
            JsonElement jetStream = root.GetProperty("NatsJetStream");
            JsonElement consumers = root.GetProperty("NatsConsumers");

            Assert.False(cleanup.GetProperty("Enabled").GetBoolean());
            Assert.True(ParseDuration(cleanup, "ProcessedInboxRetention") >=
                        ParseDuration(cleanup, "BrokerReplayHorizon"));
            Assert.True(cleanup.GetProperty("BatchSize").GetInt32() > 0);
            Assert.True(cleanup.GetProperty("MaxBatchesPerStorePerCycle").GetInt32() > 0);

            Assert.Equal("Managed", jetStream.GetProperty("ManagementMode").GetString());
            Assert.Equal("File", jetStream.GetProperty("Storage").GetString());
            Assert.True(ParseDuration(jetStream, "MaxAge") > TimeSpan.Zero);
            Assert.True(jetStream.GetProperty("MaxBytes").GetInt64() > 0);
            Assert.True(jetStream.GetProperty("MaxMessages").GetInt64() > 0);
            Assert.True(jetStream.GetProperty("Replicas").GetInt32() > 0);

            Assert.True(ParseDuration(consumers, "AckProgressInterval") <
                        ParseDuration(consumers, "AckWait"));
        }
    }

    [Fact]
    public void Worker_exposes_lease_heartbeat_and_task_history_retention_defaults()
    {
        string repositoryRoot = FindRepositoryRoot();
        using JsonDocument document = ReadAppSettings(repositoryRoot, "Host.Worker");
        JsonElement root = document.RootElement;
        JsonElement worker = root.GetProperty("Tasks").GetProperty("Worker");
        JsonElement retention = root.GetProperty("TaskRuntimeRetention");

        Assert.True(ParseDuration(worker, "HeartbeatInterval") < ParseDuration(worker, "LeaseDuration"));
        Assert.False(retention.GetProperty("Enabled").GetBoolean());
        Assert.True(retention.GetProperty("BatchSize").GetInt32() > 0);
        Assert.True(retention.GetProperty("MaxBatchesPerStatusPerCycle").GetInt32() > 0);
    }

    private static JsonDocument ReadAppSettings(string repositoryRoot, string host) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Hosts",
            host,
            "appsettings.json")));

    private static TimeSpan ParseDuration(JsonElement section, string propertyName) =>
        TimeSpan.Parse(
            section.GetProperty(propertyName).GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GMA-Skeleton.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
