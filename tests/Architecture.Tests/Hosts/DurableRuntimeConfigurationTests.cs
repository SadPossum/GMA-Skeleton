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
            Assert.True(jetStream.GetProperty("MaxMessageSize").GetInt32() > 0);
            Assert.True(jetStream.GetProperty("Replicas").GetInt32() > 0);
            Assert.Equal("Old", jetStream.GetProperty("DiscardPolicy").GetString());

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

    [Fact]
    public void Organizations_natural_expiry_is_inert_by_default_and_has_one_development_owner()
    {
        string repositoryRoot = FindRepositoryRoot();

        foreach (string host in MessagingHosts)
        {
            using JsonDocument document = ReadAppSettings(repositoryRoot, host);
            if (!document.RootElement.TryGetProperty("Organizations", out JsonElement organizations))
            {
                continue;
            }

            JsonElement lifecycle = organizations.GetProperty("Lifecycle");
            Assert.False(lifecycle.GetProperty("Enabled").GetBoolean());
            Assert.True(lifecycle.GetProperty("BatchSize").GetInt32() > 0);
            Assert.True(lifecycle.GetProperty("MaxBatchesPerCategoryPerCycle").GetInt32() > 0);
            Assert.True(lifecycle.GetProperty("IntervalMinutes").GetInt32() > 0);
        }

        using JsonDocument apiDocument = ReadAppSettings(repositoryRoot, "Host.Api");
        Assert.Equal(
            168,
            apiDocument.RootElement
                .GetProperty("Organizations")
                .GetProperty("EnrollmentClaimLifetimeHours")
                .GetInt32());

        using JsonDocument workerDocument = ReadAppSettings(repositoryRoot, "Host.Worker");
        Assert.False(
            workerDocument.RootElement
                .GetProperty("Worker")
                .GetProperty("Modules")
                .GetProperty("Organizations")
                .GetBoolean());

        using JsonDocument developmentWorkerDocument = ReadAppSettings(
            repositoryRoot,
            "Host.Worker",
            "appsettings.Development.json");
        JsonElement developmentRoot = developmentWorkerDocument.RootElement;
        Assert.True(
            developmentRoot
                .GetProperty("Worker")
                .GetProperty("Modules")
                .GetProperty("Organizations")
                .GetBoolean());
        JsonElement developmentLifecycle = developmentRoot
            .GetProperty("Organizations")
            .GetProperty("Lifecycle");
        Assert.True(developmentLifecycle.GetProperty("Enabled").GetBoolean());
        Assert.True(developmentLifecycle.GetProperty("BatchSize").GetInt32() > 0);
        Assert.True(developmentLifecycle.GetProperty("MaxBatchesPerCategoryPerCycle").GetInt32() > 0);
        Assert.True(developmentLifecycle.GetProperty("IntervalMinutes").GetInt32() > 0);
    }

    [Fact]
    public void Api_host_exposes_bounded_authentication_runtime_defaults()
    {
        string repositoryRoot = FindRepositoryRoot();
        using JsonDocument document = ReadAppSettings(repositoryRoot, "Host.Api");
        JsonElement auth = document.RootElement.GetProperty("Auth");
        JsonElement retention = auth.GetProperty("Retention");

        Assert.InRange(auth.GetProperty("MaximumActiveSessionsPerMember").GetInt32(), 1, 1000);
        Assert.True(auth.GetProperty("FailedLoginLimit").GetInt32() > 0);
        Assert.True(auth.GetProperty("FailedLoginWindowMinutes").GetInt32() > 0);
        Assert.False(retention.GetProperty("Enabled").GetBoolean());
        Assert.True(retention.GetProperty("AuthenticationFailureHistoryHours").GetInt32() > 0);
    }

    [Fact]
    public void Api_host_separates_read_traffic_from_sensitive_mutation_budgets()
    {
        string repositoryRoot = FindRepositoryRoot();
        using JsonDocument document = ReadAppSettings(repositoryRoot, "Host.Api");
        JsonElement rateLimiting = document.RootElement
            .GetProperty("Http")
            .GetProperty("RateLimiting");

        Assert.Empty(rateLimiting
            .GetProperty("SensitivePathPrefixes")
            .EnumerateArray());
        Dictionary<string, JsonElement> policies = rateLimiting
            .GetProperty("Policies")
            .EnumerateArray()
            .ToDictionary(
                policy => policy.GetProperty("Name").GetString()!,
                StringComparer.Ordinal);
        Assert.Equal(
            ["authentication-write", "organization-join-write"],
            policies.Keys.Order(StringComparer.Ordinal));

        foreach (JsonElement policy in policies.Values)
        {
            Assert.Equal(10, policy.GetProperty("PermitLimit").GetInt32());
            Assert.Equal(
                ["POST", "PUT", "PATCH", "DELETE"],
                policy.GetProperty("Methods")
                    .EnumerateArray()
                    .Select(method => method.GetString()!)
                    .ToArray());
        }

        Assert.Contains(
            policies["authentication-write"].GetProperty("PathPrefixes").EnumerateArray(),
            path => path.GetString() == "/api/auth/browser");
        Assert.Contains(
            policies["organization-join-write"].GetProperty("PathPrefixes").EnumerateArray(),
            path => path.GetString() == "/api/organization-enrollment");
    }

    [Fact]
    public void Bearer_api_hosts_require_active_auth_session_admission()
    {
        string repositoryRoot = FindRepositoryRoot();

        foreach (string host in new[] { "Host.Api", "Host.AdminApi" })
        {
            using JsonDocument document = ReadAppSettings(repositoryRoot, host);
            Assert.Equal(
                "ActiveSession",
                document.RootElement
                    .GetProperty("Auth")
                    .GetProperty("BearerAdmission")
                    .GetProperty("Mode")
                    .GetString());
        }
    }

    private static JsonDocument ReadAppSettings(
        string repositoryRoot,
        string host,
        string fileName = "appsettings.json") =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Hosts",
            host,
            fileName)));

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
