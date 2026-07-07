namespace Integration.Tests;

using DotNet.Testcontainers.Containers;
using Host.Worker;
using Integration.Tests.Support;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Gma.Framework.ModuleComposition;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class OutboxPublisherIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Member_registered_event_is_published_and_marked_processed()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_auth_tests")
            .Build();
        await nats.StartAsync();
        await postgreSql.StartAsync();

        await using AuthTestApplication application = new(
            "PostgreSql",
            postgreSql.GetConnectionString(),
            AuthTestContainers.GetNatsConnectionString(nats),
            disableOutboxPublisher: false);

        await application.MigrateDatabaseAsync();
        using HttpClient client = application.CreateClient();

        await AuthApiClient.RegisterAsync(client, "tenant-events", "events@example.com");

        int processedAfterPublish = await application.WaitForProcessedOutboxMessagesAsync(1, TimeSpan.FromSeconds(20));
        int pendingAfterPublish = await application.CountPendingOutboxMessagesAsync();

        Assert.Equal(1, processedAfterPublish);
        Assert.Equal(0, pendingAfterPublish);
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Worker_drains_auth_outbox_when_api_publishing_is_disabled()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_auth_worker_tests")
            .Build();
        await nats.StartAsync();
        await postgreSql.StartAsync();

        string natsConnectionString = AuthTestContainers.GetNatsConnectionString(nats);
        await using AuthTestApplication application = new(
            "PostgreSql",
            postgreSql.GetConnectionString(),
            natsConnectionString,
            disableOutboxPublisher: true);
        using IHost worker = BuildAuthWorker(
            postgreSql.GetConnectionString(),
            natsConnectionString,
            $"GMA_WORKER_{Guid.NewGuid():N}".ToUpperInvariant());

        await application.MigrateDatabaseAsync();
        using HttpClient client = application.CreateClient();

        await AuthApiClient.RegisterAsync(client, "tenant-worker", "worker@example.com");
        Assert.Equal(1, await application.CountPendingOutboxMessagesAsync().ConfigureAwait(false));

        await worker.StartAsync().ConfigureAwait(false);
        try
        {
            int processedAfterWorkerPublish =
                await application.WaitForProcessedOutboxMessagesAsync(1, TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            int pendingAfterWorkerPublish = await application.CountPendingOutboxMessagesAsync().ConfigureAwait(false);

            Assert.Equal(1, processedAfterWorkerPublish);
            Assert.Equal(0, pendingAfterWorkerPublish);
        }
        finally
        {
            await worker.StopAsync().ConfigureAwait(false);
        }
    }

    private static IHost BuildAuthWorker(
        string postgreSqlConnectionString,
        string natsConnectionString,
        string streamName)
    {
        HostApplicationBuilder builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Integration",
        });
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] = postgreSqlConnectionString;
        builder.Configuration["ConnectionStrings:nats"] = natsConnectionString;
        builder.Configuration["Tenancy:Enabled"] = "true";
        builder.Configuration["NatsJetStream:Enabled"] = "true";
        builder.Configuration["NatsJetStream:StreamName"] = streamName;
        builder.Configuration["Outbox:BatchSize"] = "5";
        builder.Configuration["Outbox:PollIntervalMilliseconds"] = "100";
        builder.Configuration["Outbox:LockDurationMilliseconds"] = "1000";
        builder.Configuration["Worker:Modules:Auth"] = "true";
        builder.Logging.ClearProviders();

        builder.AddWorkerHost();
        builder.ValidateModuleComposition();

        return builder.Build();
    }
}
