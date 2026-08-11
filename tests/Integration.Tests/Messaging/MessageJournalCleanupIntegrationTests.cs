namespace Integration.Tests;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Infrastructure;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping.Infrastructure;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ordering.Persistence;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class MessageJournalCleanupIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Bounded_processed_journal_cleanup_runs_on_sql_server_and_postgre_sql()
    {
        await using MsSqlContainer sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sqlServer.StartAsync();
        await RunScenarioAsync(
            "SqlServer",
            AuthTestContainers.UseDatabase(sqlServer.GetConnectionString(), "gma_journal_cleanup_tests"));

        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_journal_cleanup_tests")
            .Build();
        await postgreSql.StartAsync();
        await RunScenarioAsync("PostgreSql", postgreSql.GetConnectionString());
    }

    private static async Task RunScenarioAsync(string provider, string connectionString)
    {
        await using ServiceProvider serviceProvider = BuildProvider(provider, connectionString);
        using IServiceScope scope = serviceProvider.CreateScope();
        OrderingDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);

        OutboxMessage oldOutboxA = CreateOutbox(Guid.NewGuid(), Now.AddDays(-10));
        OutboxMessage oldOutboxB = CreateOutbox(Guid.NewGuid(), Now.AddDays(-9));
        OutboxMessage recentOutbox = CreateOutbox(Guid.NewGuid(), Now.AddDays(-1));
        OutboxMessage exhaustedOutbox = new(
            Guid.NewGuid(),
            "gma.cleanup.test.v1",
            "cleanup-test",
            1,
            null,
            Now.AddDays(-10),
            "{}",
            Now.AddDays(-10));
        exhaustedOutbox.MarkClaimed("worker-a", Now.AddDays(-9), TimeSpan.FromMinutes(1));
        exhaustedOutbox.MarkFailed("permanent", Now.AddDays(-9), maxAttempts: 1);

        InboxMessage oldInboxA = CreateInbox(Guid.NewGuid(), Now.AddDays(-10), processed: true);
        InboxMessage oldInboxB = CreateInbox(Guid.NewGuid(), Now.AddDays(-9), processed: true);
        InboxMessage recentInbox = CreateInbox(Guid.NewGuid(), Now.AddDays(-1), processed: true);
        InboxMessage failedInbox = CreateInbox(Guid.NewGuid(), Now.AddDays(-10), processed: false);

        dbContext.AddRange(
            oldOutboxA,
            oldOutboxB,
            recentOutbox,
            exhaustedOutbox,
            oldInboxA,
            oldInboxB,
            recentInbox,
            failedInbox);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        TestOutboxStore outboxStore = new(dbContext);
        TestInboxStore inboxStore = new(dbContext);
        DateTimeOffset cutoff = Now.AddDays(-7);

        int deletedOutbox = await outboxStore.DeleteProcessedBeforeAsync(cutoff, 1, CancellationToken.None)
            .ConfigureAwait(false);
        int deletedInbox = await inboxStore.DeleteProcessedBeforeAsync(cutoff, 1, CancellationToken.None)
            .ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();

        Assert.Equal(1, deletedOutbox);
        Assert.Equal(1, deletedInbox);
        Assert.Equal(Now.AddDays(-9), await outboxStore.GetOldestProcessedAtUtcAsync(CancellationToken.None));
        Assert.Equal(Now.AddDays(-9), await inboxStore.GetOldestProcessedAtUtcAsync(CancellationToken.None));
        Assert.Equal(3, await dbContext.OutboxMessages.CountAsync().ConfigureAwait(false));
        Assert.Equal(3, await dbContext.InboxMessages.CountAsync().ConfigureAwait(false));
        Assert.True(await dbContext.OutboxMessages.AnyAsync(message => message.Id == recentOutbox.Id));
        Assert.True(await dbContext.OutboxMessages.AnyAsync(message => message.Id == exhaustedOutbox.Id));
        Assert.True(await dbContext.InboxMessages.AnyAsync(message => message.Id == recentInbox.Id));
        Assert.True(await dbContext.InboxMessages.AnyAsync(message => message.Id == failedInbox.Id));
    }

    private static OutboxMessage CreateOutbox(Guid id, DateTimeOffset processedAtUtc)
    {
        OutboxMessage message = new(
            id,
            "gma.cleanup.test.v1",
            "cleanup-test",
            1,
            null,
            processedAtUtc.AddMinutes(-1),
            "{}",
            processedAtUtc.AddMinutes(-1));
        message.MarkClaimed("worker-a", processedAtUtc.AddMinutes(-1), TimeSpan.FromMinutes(2));
        message.MarkProcessed(processedAtUtc);
        return message;
    }

    private static InboxMessage CreateInbox(Guid id, DateTimeOffset completedAtUtc, bool processed)
    {
        InboxMessage message = InboxMessage.Create(
            id,
            "cleanup-handler",
            "gma.cleanup.test.v1",
            "cleanup-test",
            1,
            null,
            completedAtUtc.AddMinutes(-1),
            completedAtUtc.AddMinutes(-1));
        message.MarkProcessing("worker-a", completedAtUtc.AddSeconds(-1));
        if (processed)
        {
            message.MarkProcessed(completedAtUtc);
        }
        else
        {
            message.MarkFailed("permanent", completedAtUtc);
        }

        return message;
    }

    private static ServiceProvider BuildProvider(string provider, string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = provider;
        builder.Configuration[$"ConnectionStrings:{provider}"] = connectionString;
        builder.Configuration["Tenancy:Enabled"] = "false";
        builder.AddRuntimeInfrastructure();
        builder.AddScopingInfrastructure();
        builder.AddOrderingPersistence();

        return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class TestOutboxStore(OrderingDbContext dbContext)
        : EfOutboxStore<OrderingDbContext>(
            dbContext,
            Options.Create(new OutboxOptions()),
            OrderingMigrations.Schema);

    private sealed class TestInboxStore(OrderingDbContext dbContext)
        : EfInboxStore<OrderingDbContext>(
            dbContext,
            new FixedClock(),
            new TestIdGenerator(),
            OrderingMigrations.Schema);

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

}
