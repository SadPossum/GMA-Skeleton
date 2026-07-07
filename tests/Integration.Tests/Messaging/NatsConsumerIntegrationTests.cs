namespace Integration.Tests;

using Catalog.Contracts;
using System.Collections.Concurrent;
using DotNet.Testcontainers.Containers;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Gma.Framework.Messaging.Infrastructure;
using Ordering.Application;
using Ordering.Persistence;
using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Nats;
using Gma.Framework.Runtime;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Infrastructure;
using Gma.Framework.Tenancy.Messaging.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class NatsConsumerIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Catalog_event_is_consumed_into_ordering_projection_and_inbox()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_ordering_consumer_tests")
            .Build();
        await nats.StartAsync();
        await postgreSql.StartAsync();

        string tenantId = "tenant-consumer";
        string streamName = $"GMA_CONSUMER_{Guid.NewGuid():N}".ToUpperInvariant();
        ConcurrentQueue<string> logs = new();
        using IHost host = BuildHost(
            postgreSql.GetConnectionString(),
            AuthTestContainers.GetNatsConnectionString(nats),
            streamName,
            logs);

        await MigrateAsync(host).ConfigureAwait(false);

        Assert.True(host.Services.GetRequiredService<IOptions<NatsConsumerOptions>>().Value.Enabled);
        Assert.NotEmpty(host.Services.GetRequiredService<IIntegrationEventSubscriptionRegistry>().Subscriptions);

        await host.StartAsync().ConfigureAwait(false);

        try
        {
            Guid itemId = Guid.NewGuid();
            CatalogItemCreatedIntegrationEvent integrationEvent = new(
                Guid.NewGuid(),
                tenantId,
                new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero),
                itemId,
                "SKU-42",
                "Projection item",
                42.50m,
                "USD");
            IntegrationEventEnvelope envelope = IntegrationEventEnvelopeFactory.Create("catalog", integrationEvent);
            IEventBus eventBus = host.Services.GetRequiredService<IEventBus>();

            await eventBus.PublishAsync(
                    new OutboxMessageRecord(
                        envelope.EventId,
                        envelope.Subject,
                        envelope.EventType,
                        envelope.Version,
                        envelope.ScopeId,
                        envelope.OccurredAtUtc,
                        envelope.Payload),
                    CancellationToken.None)
                .ConfigureAwait(false);

            await WaitForProjectionAsync(host, tenantId, itemId, logs, TimeSpan.FromSeconds(20)).ConfigureAwait(false);

            using IServiceScope scope = host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(tenantId);
            OrderingDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();

            CatalogItemProjection projection = await dbContext.CatalogItemProjections.SingleAsync(
                    item => item.CatalogItemId == itemId,
                    CancellationToken.None)
                .ConfigureAwait(false);
            InboxMessage inbox = await dbContext.InboxMessages.SingleAsync(
                    message => message.Id == integrationEvent.EventId &&
                               message.Handler == "catalog-item-created-projection",
                    CancellationToken.None)
                .ConfigureAwait(false);

            Assert.Equal("SKU-42", projection.Sku);
            Assert.Equal("Projection item", projection.Name);
            Assert.Equal(CatalogItemStatus.Active, projection.Status);
            Assert.Equal(InboxMessageStatus.Processed, inbox.Status);
            Assert.NotNull(inbox.ProcessedAtUtc);
        }
        finally
        {
            await host.StopAsync().ConfigureAwait(false);
        }
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Event_bus_publish_succeeds_when_logger_throws_after_broker_ack()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();

        string streamName = $"GMA_LOGGER_{Guid.NewGuid():N}".ToUpperInvariant();
        await using NatsConnection connection = new(new NatsOpts
        {
            Url = AuthTestContainers.GetNatsConnectionString(nats),
        });
        NatsJetStreamEventBus firstBus = new(
            connection,
            Options.Create(new NatsJetStreamOptions { StreamName = streamName }),
            Options.Create(new ApplicationIdentityOptions()),
            new ThrowingLogger<NatsJetStreamEventBus>());

        await firstBus.PublishAsync(CreateMessage("tenant-logger", "logger-1"), CancellationToken.None).ConfigureAwait(false);

        NatsJetStreamEventBus secondBus = new(
            connection,
            Options.Create(new NatsJetStreamOptions { StreamName = streamName }),
            Options.Create(new ApplicationIdentityOptions()),
            new ThrowingLogger<NatsJetStreamEventBus>());

        await secondBus.PublishAsync(CreateMessage("tenant-logger", "logger-2"), CancellationToken.None).ConfigureAwait(false);
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Event_bus_uses_outbox_message_id_for_jetstream_deduplication()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();

        string streamName = $"GMA_DEDUPE_{Guid.NewGuid():N}".ToUpperInvariant();
        await using NatsConnection connection = new(new NatsOpts
        {
            Url = AuthTestContainers.GetNatsConnectionString(nats),
        });
        NatsJetStreamEventBus eventBus = new(
            connection,
            Options.Create(new NatsJetStreamOptions { StreamName = streamName }),
            Options.Create(new ApplicationIdentityOptions()),
            NullLogger<NatsJetStreamEventBus>.Instance);
        OutboxMessageRecord message = CreateMessage("tenant-dedupe", "dedupe");

        await eventBus.PublishAsync(message, CancellationToken.None).ConfigureAwait(false);
        await eventBus.PublishAsync(message, CancellationToken.None).ConfigureAwait(false);

        NatsJSContext jetStream = new(connection);
        INatsJSConsumer consumer = await jetStream
            .CreateOrUpdateConsumerAsync(
                streamName,
                new ConsumerConfig($"dedupe-{Guid.NewGuid():N}")
                {
                    FilterSubject = message.Subject,
                    AckWait = TimeSpan.FromSeconds(5),
                    MaxDeliver = 1,
                    MaxAckPending = 10
                },
                CancellationToken.None)
            .ConfigureAwait(false);
        List<INatsJSMsg<string>> storedMessages = [];

        await foreach (INatsJSMsg<string> storedMessage in consumer.FetchAsync(
                           new NatsJSFetchOpts
                           {
                               MaxMsgs = 10,
                               Expires = TimeSpan.FromSeconds(2)
                           },
                           NatsDefaultSerializer<string>.Default,
                           CancellationToken.None)
                       .ConfigureAwait(false))
        {
            storedMessages.Add(storedMessage);
            await storedMessage.AckAsync(cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }

        Assert.Single(storedMessages);
        Assert.Equal(message.Payload, storedMessages[0].Data);
    }

    private static IHost BuildHost(
        string postgreSqlConnectionString,
        string natsConnectionString,
        string streamName,
        ConcurrentQueue<string> logs)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Environment.EnvironmentName = "Integration";
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] = postgreSqlConnectionString;
        builder.Configuration["Tenancy:Enabled"] = "true";
        builder.Configuration["NatsJetStream:StreamName"] = streamName;
        builder.Configuration["NatsConsumers:Enabled"] = "true";
        builder.Configuration["NatsConsumers:FetchBatchSize"] = "1";
        builder.Configuration["NatsConsumers:PollInterval"] = "00:00:00.100";
        builder.Configuration["NatsConsumers:AckWait"] = "00:00:05";
        builder.Configuration["NatsConsumers:HandlerTimeout"] = "00:00:05";
        builder.Configuration["NatsConsumers:NakDelay"] = "00:00:00.100";
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new QueueLoggerProvider(logs));

        builder.Services.AddSingleton<INatsConnection>(_ => new NatsConnection(new NatsOpts
        {
            Url = natsConnectionString,
        }));

        builder.AddTenancyInfrastructure();
        builder.AddTenantAwareMessaging();
        builder.Services.AddOrderingApplication();
        builder.AddOrderingPersistence();
        builder.AddNatsJetStreamMessaging();
        builder.AddNatsJetStreamConsumers();

        return builder.Build();
    }

    private static OutboxMessageRecord CreateMessage(string scopeId, string suffix) =>
        new(
            Guid.NewGuid(),
            "gma.test.logger.v1",
            "Integration.Tests.LoggerEvent",
            1,
            scopeId,
            DateTimeOffset.UtcNow,
            $$"""{"suffix":"{{suffix}}"}""");

    private static async Task MigrateAsync(IHost host)
    {
        using IServiceScope scope = host.Services.CreateScope();
        OrderingDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    private static async Task WaitForProjectionAsync(
        IHost host,
        string tenantId,
        Guid itemId,
        ConcurrentQueue<string> logs,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            using IServiceScope scope = host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(tenantId);
            OrderingDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();

            bool exists = await dbContext.CatalogItemProjections
                .AnyAsync(item => item.CatalogItemId == itemId)
                .ConfigureAwait(false);

            if (exists)
            {
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        string diagnostics = await CreateDiagnosticsAsync(host, tenantId, itemId, logs).ConfigureAwait(false);
        throw new TimeoutException($"Ordering projection for catalog item '{itemId}' was not created. {diagnostics}");
    }

    private static async Task<string> CreateDiagnosticsAsync(
        IHost host,
        string tenantId,
        Guid itemId,
        ConcurrentQueue<string> logs)
    {
        using IServiceScope scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(tenantId);
        OrderingDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();

        int projectionCount = await dbContext.CatalogItemProjections
            .CountAsync(item => item.CatalogItemId == itemId)
            .ConfigureAwait(false);
        string[] inboxStates = await dbContext.InboxMessages
            .OrderBy(message => message.Handler)
            .Select(message => $"{message.Handler}:{message.Status}:{message.Attempts}:{message.LastError}")
            .ToArrayAsync()
            .ConfigureAwait(false);
        string[] recentLogs = logs.TakeLast(20).ToArray();

        return $"ProjectionCount={projectionCount}; Inbox=[{string.Join(", ", inboxStates)}]; Logs=[{string.Join(" | ", recentLogs)}]";
    }

    private sealed class QueueLoggerProvider(ConcurrentQueue<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new QueueLogger(logs, categoryName);

        public void Dispose()
        {
        }
    }

    private sealed class QueueLogger(ConcurrentQueue<string> logs, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= LogLevel.Warning ||
            (logLevel >= LogLevel.Information &&
             categoryName.Contains("NatsJetStreamConsumerService", StringComparison.Ordinal));

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            logs.Enqueue($"{logLevel}:{categoryName}:{formatter(state, exception)}:{exception?.GetType().Name}:{exception?.Message}");
        }
    }

    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("Logger unavailable.");
    }
}
