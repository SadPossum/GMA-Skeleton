namespace Integration.Tests;

using System.Net;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Gma.Framework.RateLimiting;
using Gma.Framework.RateLimiting.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

[Trait("Category", "Integration")]
public sealed class RedisRateLimitingIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task Redis_enforces_atomic_cross_instance_quotas_under_concurrency()
    {
        IContainer redis = new ContainerBuilder("redis:7.4-alpine")
            .WithPortBinding(6379, assignRandomHostPort: true)
            .Build();

        try
        {
            using CancellationTokenSource startupTimeout =
                new(TimeSpan.FromSeconds(60));
            await redis.StartAsync(startupTimeout.Token)
                .WaitAsync(TimeSpan.FromSeconds(90));
            int redisPort = redis.GetMappedPublicPort(6379);
            await WaitForTcpPortAsync(redisPort);
            string connectionString =
                $"127.0.0.1:{redisPort},abortConnect=false,connectTimeout=1000,syncTimeout=1000";
            await using ServiceProvider firstProvider =
                BuildProvider(connectionString);
            await using ServiceProvider secondProvider =
                BuildProvider(connectionString);
            IMultiPartitionRateLimiter first =
                firstProvider.GetRequiredService<IMultiPartitionRateLimiter>();
            IMultiPartitionRateLimiter second =
                secondProvider.GetRequiredService<IMultiPartitionRateLimiter>();

            FixedWindowRateLimitPartition constrained =
                Partition("principal:atomic", permitLimit: 1);
            FixedWindowRateLimitPartition shared =
                Partition("tenant:atomic", permitLimit: 10);
            var combined = new MultiPartitionRateLimitRequest(
                "workload:atomic",
                permitCount: 1,
                [constrained, shared]);

            Assert.Equal(
                MultiPartitionRateLimitOutcome.Acquired,
                (await WithTimeout(first.AcquireAsync(combined))).Outcome);
            MultiPartitionRateLimitDecision rejected =
                await WithTimeout(second.AcquireAsync(combined));
            Assert.Equal(
                MultiPartitionRateLimitOutcome.Rejected,
                rejected.Outcome);
            Assert.InRange(
                rejected.RetryAfter ?? TimeSpan.Zero,
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMinutes(1));

            var remainingSharedCapacity = new MultiPartitionRateLimitRequest(
                "workload:atomic",
                permitCount: 9,
                [shared]);
            Assert.Equal(
                MultiPartitionRateLimitOutcome.Acquired,
                (await WithTimeout(
                    second.AcquireAsync(remainingSharedCapacity))).Outcome);

            var concurrent = new MultiPartitionRateLimitRequest(
                "workload:concurrent",
                permitCount: 1,
                [
                    Partition("principal:concurrent", permitLimit: 25),
                    Partition("tenant:concurrent", permitLimit: 25)
                ]);
            Task<MultiPartitionRateLimitDecision>[] attempts =
                Enumerable.Range(0, 100)
                    .Select(index => WithTimeout(
                        (index % 2 == 0 ? first : second)
                            .AcquireAsync(concurrent)))
                    .ToArray();
            MultiPartitionRateLimitDecision[] decisions =
                await Task.WhenAll(attempts);

            Assert.Equal(
                25,
                decisions.Count(decision =>
                    decision.Outcome ==
                    MultiPartitionRateLimitOutcome.Acquired));
            Assert.Equal(
                75,
                decisions.Count(decision =>
                    decision.Outcome ==
                    MultiPartitionRateLimitOutcome.Rejected));
        }
        finally
        {
            await redis.DisposeAsync()
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Environment.EnvironmentName = "RateLimitTests";
        builder.Configuration["ApplicationIdentity:Namespace"] = "gma-tests";
        builder.Configuration["ConnectionStrings:redis"] = connectionString;
        builder.AddRedisRateLimiting();

        return builder.Services.BuildServiceProvider();
    }

    private static FixedWindowRateLimitPartition Partition(
        string identity,
        int permitLimit) =>
        new(identity, permitLimit, TimeSpan.FromMinutes(1));

    private static Task<MultiPartitionRateLimitDecision> WithTimeout(
        ValueTask<MultiPartitionRateLimitDecision> operation) =>
        operation.AsTask().WaitAsync(TimeSpan.FromSeconds(15));

    private static async Task WaitForTcpPortAsync(int port)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        Exception? lastException = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using TcpClient client = new();
                await client
                    .ConnectAsync(IPAddress.Loopback, port)
                    .WaitAsync(TimeSpan.FromSeconds(1));
                return;
            }
            catch (Exception exception) when (
                exception is SocketException or TimeoutException)
            {
                lastException = exception;
                await Task.Delay(100);
            }
        }

        throw new TimeoutException(
            $"Redis did not become reachable on mapped port {port}.",
            lastException);
    }
}
