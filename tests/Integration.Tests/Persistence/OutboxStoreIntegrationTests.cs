namespace Integration.Tests;

using DotNet.Testcontainers.Containers;
using Integration.Tests.Support;
using Gma.Framework.Messaging;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class OutboxStoreIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Outbox_store_claims_retries_reclaims_and_marks_messages()
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
            AuthTestContainers.GetNatsConnectionString(nats));

        await application.MigrateDatabaseAsync();

        DateTimeOffset nowUtc = new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);
        Guid retryMessageId = await application.AddOutboxMessageAsync(nowUtc);

        IReadOnlyList<OutboxMessageRecord> workerAClaim = await application.ClaimOutboxAsync(
            "worker-a",
            nowUtc,
            TimeSpan.FromSeconds(10));
        IReadOnlyList<OutboxMessageRecord> workerBBlockedClaim = await application.ClaimOutboxAsync(
            "worker-b",
            nowUtc,
            TimeSpan.FromSeconds(10));

        await application.MarkOutboxProcessedAsync(retryMessageId, "worker-b", nowUtc.AddSeconds(1));
        OutboxSnapshot afterWrongWorkerMark = await application.GetOutboxSnapshotAsync(retryMessageId);

        await application.MarkOutboxFailedAsync(retryMessageId, "worker-a", "temporary", nowUtc.AddSeconds(1));
        OutboxSnapshot afterFailure = await application.GetOutboxSnapshotAsync(retryMessageId);

        IReadOnlyList<OutboxMessageRecord> beforeRetryDueClaim = await application.ClaimOutboxAsync(
            "worker-b",
            nowUtc.AddSeconds(2),
            TimeSpan.FromSeconds(10));
        IReadOnlyList<OutboxMessageRecord> afterRetryDueClaim = await application.ClaimOutboxAsync(
            "worker-b",
            nowUtc.AddSeconds(4),
            TimeSpan.FromSeconds(10));

        await application.MarkOutboxProcessedAsync(retryMessageId, "worker-b", nowUtc.AddSeconds(5));
        OutboxSnapshot afterProcessed = await application.GetOutboxSnapshotAsync(retryMessageId);

        Guid reclaimMessageId = await application.AddOutboxMessageAsync(nowUtc.AddSeconds(10));
        await application.ClaimOutboxAsync("worker-a", nowUtc.AddSeconds(10), TimeSpan.FromSeconds(1));
        IReadOnlyList<OutboxMessageRecord> afterLockExpiredClaim = await application.ClaimOutboxAsync(
            "worker-b",
            nowUtc.AddSeconds(12),
            TimeSpan.FromSeconds(10));

        Assert.Single(workerAClaim);
        Assert.Empty(workerBBlockedClaim);
        Assert.Null(afterWrongWorkerMark.ProcessedAtUtc);
        Assert.Equal(1, afterFailure.Attempts);
        Assert.NotNull(afterFailure.NextAttemptAtUtc);
        Assert.Empty(beforeRetryDueClaim);
        Assert.Single(afterRetryDueClaim);
        Assert.Equal(nowUtc.AddSeconds(5), afterProcessed.ProcessedAtUtc);
        Assert.Single(afterLockExpiredClaim);
        Assert.Equal(reclaimMessageId, afterLockExpiredClaim[0].Id);
    }
}
