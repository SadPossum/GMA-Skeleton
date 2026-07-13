namespace Integration.Tests;

using Gma.Framework.Notifications;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Application;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Domain.Entities;
using Gma.Modules.Notifications.Persistence;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;
using DomainAttemptOutcome = Gma.Modules.Notifications.Domain.ValueObjects.NotificationDeliveryAttemptOutcome;
using DomainDeliveryStatus = Gma.Modules.Notifications.Domain.ValueObjects.NotificationDeliveryStatus;
using DomainSeverity = Gma.Modules.Notifications.Domain.ValueObjects.NotificationSeverity;

public sealed class NotificationsPersistenceIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 16, 0, 0, TimeSpan.Zero);

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Migrations_claims_and_retention_queries_run_against_sql_server_and_postgre_sql()
    {
        await using MsSqlContainer sqlServer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
        await sqlServer.StartAsync();
        await RunProviderAsync(
            "SqlServer",
            AuthTestContainers.UseDatabase(sqlServer.GetConnectionString(), "gma_notifications_tests"));

        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("gma_notifications_tests")
            .Build();
        await postgreSql.StartAsync();
        await RunProviderAsync("PostgreSql", postgreSql.GetConnectionString());
    }

    private static async Task RunProviderAsync(string provider, string connectionString)
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton<IScopeContext>(new TestScopeContext("tenant-a"));
        services.AddDbContext<NotificationsDbContext>(options => ConfigureProvider(
            options,
            provider,
            connectionString));
        await using ServiceProvider serviceProvider = services.BuildServiceProvider();

        Guid activeNotificationId = Guid.CreateVersion7();
        Guid completedNotificationId = Guid.CreateVersion7();
        Guid activeDeliveryId = Guid.CreateVersion7();
        Guid completedDeliveryId = Guid.CreateVersion7();
        Guid activeAttemptId = Guid.CreateVersion7();
        Guid completedAttemptId = Guid.CreateVersion7();
        DateTimeOffset old = Now.AddDays(-400);

        await using (AsyncServiceScope seedScope = serviceProvider.CreateAsyncScope())
        {
            NotificationsDbContext dbContext = seedScope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
            await dbContext.Database.MigrateAsync();

            UserNotification activeNotification = CreateNotification(activeNotificationId, "auth", old);
            UserNotification completedNotification = CreateNotification(completedNotificationId, "catalog", old);
            NotificationDelivery activeDelivery = NotificationDelivery.CreatePending(
                activeDeliveryId,
                "tenant-a",
                activeNotification.Id,
                NotificationTags.Email,
                "email-primary",
                old).Value;
            NotificationDelivery completedDelivery = NotificationDelivery.CreateDelivered(
                completedDeliveryId,
                "tenant-a",
                completedNotification.Id,
                NotificationTags.Email,
                "email-primary",
                old).Value;
            NotificationDeliveryAttempt activeAttempt = NotificationDeliveryAttempt.Create(
                activeAttemptId,
                "tenant-a",
                activeDelivery.Id,
                1,
                "email-primary",
                DomainAttemptOutcome.Retry,
                old,
                old.AddSeconds(1),
                "rate-limited",
                providerMessageId: null).Value;
            NotificationDeliveryAttempt completedAttempt = NotificationDeliveryAttempt.Create(
                completedAttemptId,
                "tenant-a",
                completedDelivery.Id,
                1,
                "email-primary",
                DomainAttemptOutcome.Delivered,
                old,
                old.AddSeconds(1),
                code: null,
                providerMessageId: "provider-message-1").Value;

            dbContext.UserNotifications.AddRange(activeNotification, completedNotification);
            dbContext.NotificationDeliveries.AddRange(activeDelivery, completedDelivery);
            dbContext.NotificationDeliveryAttempts.AddRange(activeAttempt, completedAttempt);
            await dbContext.SaveChangesAsync();
        }

        NotificationDeliveryOptions deliveryOptions = new()
        {
            WorkerId = $"notifications-{provider.ToLowerInvariant()}",
            BatchSize = 10,
            MaxConcurrency = 4,
            LeaseSeconds = 60
        };
        NotificationDeliveryMetrics metrics = new(
            serviceProvider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>(),
            Options.Create(new ApplicationIdentityOptions { Namespace = "notification-tests" }));
        NotificationDeliveryService worker = new(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            new EmptyDeliveryAdapterCatalog(),
            new FixedClock(Now),
            new TestIdGenerator(),
            Options.Create(deliveryOptions),
            metrics,
            NullLogger<NotificationDeliveryService>.Instance);

        Guid claimedId = Assert.Single(await worker.ClaimAsync(CancellationToken.None));

        await using AsyncServiceScope assertionScope = serviceProvider.CreateAsyncScope();
        NotificationsDbContext assertionDb = assertionScope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        UserNotification[] expiredNotifications = await NotificationRetentionService
            .ExpiredUserNotifications(assertionDb, Now.AddDays(-90), Now.AddDays(-365))
            .ToArrayAsync();
        NotificationDeliveryAttempt[] expiredAttempts = await NotificationRetentionService
            .ExpiredDeliveryAttempts(assertionDb, Now.AddDays(-90))
            .ToArrayAsync();
        NotificationDelivery claimed = await assertionDb.NotificationDeliveries
            .IgnoreQueryFilters()
            .SingleAsync(delivery => delivery.Id == claimedId);

        Assert.Equal(activeDeliveryId, claimedId);
        Assert.Equal(DomainDeliveryStatus.Processing, claimed.Status);
        Assert.NotNull(claimed.LockedUntilUtc);
        Assert.Equal(completedNotificationId, Assert.Single(expiredNotifications).Id);
        Assert.Equal(completedAttemptId, Assert.Single(expiredAttempts).Id);
    }

    private static void ConfigureProvider(
        DbContextOptionsBuilder options,
        string provider,
        string connectionString)
    {
        if (provider == "SqlServer")
        {
            options.UseSqlServer(
                connectionString,
                sqlServer => sqlServer
                    .MigrationsAssembly(NotificationsMigrations.SqlServerAssembly)
                    .MigrationsHistoryTable(NotificationsMigrations.HistoryTable, NotificationsMigrations.Schema));
            return;
        }

        options.UseNpgsql(
            connectionString,
            postgreSql => postgreSql
                .MigrationsAssembly(NotificationsMigrations.PostgreSqlAssembly)
                .MigrationsHistoryTable(NotificationsMigrations.HistoryTable, NotificationsMigrations.Schema));
    }

    private static UserNotification CreateNotification(Guid id, string module, DateTimeOffset createdAtUtc) =>
        UserNotification.Create(
            id,
            "tenant-a",
            "user-a",
            module,
            $"{module}.notification",
            2,
            "Notification",
            null,
            DomainSeverity.Info,
            createdAtUtc,
            createdAtUtc,
            "{}",
            [NotificationTags.Email],
            Gma.Modules.Notifications.Domain.ValueObjects.NotificationDeliveryPolicy.Mandatory,
            isInboxVisible: false).Value;

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }

    private sealed class EmptyDeliveryAdapterCatalog : INotificationDeliveryAdapterCatalog
    {
        public IReadOnlyList<string> GetProviders(string deliveryTag) => [];
        public bool Supports(string provider, string deliveryTag) => false;
        public IUserNotificationSink? GetProvider(string provider) => null;
    }

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId => scopeId;
    }
}
