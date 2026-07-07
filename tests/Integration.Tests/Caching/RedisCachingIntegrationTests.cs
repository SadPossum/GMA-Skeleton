namespace Integration.Tests;

using System.Net;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Cqrs;
using Gma.Framework.Caching;
using Gma.Framework.Caching.Cqrs;
using Gma.Framework.Caching.Redis;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Integration")]
public sealed class RedisCachingIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    public async Task Redis_provides_cross_instance_reads_expiration_and_invalidation()
    {
        IContainer redis = new ContainerBuilder("redis:7.4-alpine")
            .WithPortBinding(6379, assignRandomHostPort: true)
            .Build();

        try
        {
            using CancellationTokenSource startupTimeout = new(TimeSpan.FromSeconds(60));
            await redis.StartAsync(startupTimeout.Token).WaitAsync(TimeSpan.FromSeconds(90));
            int redisPort = redis.GetMappedPublicPort(6379);
            await WaitForTcpPortAsync(redisPort);
            string connectionString =
                $"127.0.0.1:{redisPort},abortConnect=false,connectTimeout=1000,syncTimeout=1000";
            using ServiceProvider firstProvider = BuildProvider(connectionString);
            using ServiceProvider secondProvider = BuildProvider(connectionString);
            CacheEntryPolicy distributedOnly = new(
                TimeSpan.FromMinutes(1),
                TimeSpan.FromSeconds(1),
                localCacheEnabled: false);

            using IServiceScope firstScope = firstProvider.CreateScope();
            using IServiceScope secondScope = secondProvider.CreateScope();
            IApplicationCache first = firstScope.ServiceProvider.GetRequiredService<IApplicationCache>();
            IApplicationCache second = secondScope.ServiceProvider.GetRequiredService<IApplicationCache>();
            IDistributedCache configuredDistributedCache = firstScope.ServiceProvider.GetRequiredService<IDistributedCache>();
            Assert.Contains("Redis", configuredDistributedCache.GetType().Name, StringComparison.Ordinal);

            CacheKey sharedKey = CacheKey.Global("catalog", "cross-instance");
            int sharedCalls = 0;
            int created = await WithTimeout(
                first.GetOrCreateAsync(
                    sharedKey,
                    _ => ValueTask.FromResult(++sharedCalls),
                    distributedOnly),
                "populate cross-instance cache entry");
            await WaitForDistributedValueAsync(
                configuredDistributedCache,
                "gma:redistests:catalog:global:global:cross-instance");
            int distributedHit = await WithTimeout(
                second.GetOrCreateAsync(
                    sharedKey,
                    _ => ValueTask.FromResult(++sharedCalls),
                    distributedOnly),
                "read cross-instance cache entry");
            Assert.Equal(created, distributedHit);
            Assert.Equal(1, sharedCalls);

            CacheKey expiringKey = CacheKey.Global("catalog", "expiring");
            CacheEntryPolicy expiring = new(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromMilliseconds(100),
                localCacheEnabled: false);
            int expirationCalls = 0;
            await WithTimeout(
                first.GetOrCreateAsync(expiringKey, _ => ValueTask.FromResult(++expirationCalls), expiring),
                "populate expiring cache entry");
            await WaitForDistributedValueAsync(
                configuredDistributedCache,
                "gma:redistests:catalog:global:global:expiring");
            await Task.Delay(1200);
            await WithTimeout(
                second.GetOrCreateAsync(expiringKey, _ => ValueTask.FromResult(++expirationCalls), expiring),
                "read expired cache entry");
            Assert.Equal(2, expirationCalls);

            CacheKey invalidatedKey = CacheKey.Global("catalog", "invalidate-key");
            int keyCalls = 0;
            await WithTimeout(
                first.GetOrCreateAsync(invalidatedKey, _ => ValueTask.FromResult(++keyCalls), distributedOnly),
                "populate key invalidation entry");
            await WaitForDistributedValueAsync(
                configuredDistributedCache,
                "gma:redistests:catalog:global:global:invalidate-key");
            await WithTimeout(
                second.GetOrCreateAsync(invalidatedKey, _ => ValueTask.FromResult(++keyCalls), distributedOnly),
                "read key invalidation entry before invalidation");
            ICacheInvalidationQueue firstQueue = firstScope.ServiceProvider.GetRequiredService<ICacheInvalidationQueue>();
            firstQueue.Remove(invalidatedKey);
            await WithTimeout(
                SendSuccessfulCommandAsync(firstScope.ServiceProvider),
                "flush key invalidation");
            await WithTimeout(
                second.GetOrCreateAsync(invalidatedKey, _ => ValueTask.FromResult(++keyCalls), distributedOnly),
                "read key invalidation entry after invalidation");
            Assert.Equal(2, keyCalls);

            CacheTag products = CacheTag.Global("catalog", "products");
            CacheKey taggedKey = CacheKey.Global("catalog", "tagged", "42");
            int tagCalls = 0;
            await WithTimeout(
                first.GetOrCreateAsync(taggedKey, _ => ValueTask.FromResult(++tagCalls), distributedOnly, [products]),
                "populate tagged cache entry");
            await WaitForDistributedValueAsync(
                configuredDistributedCache,
                "gma:redistests:catalog:global:global:tagged:42");
            await WithTimeout(
                second.GetOrCreateAsync(taggedKey, _ => ValueTask.FromResult(++tagCalls), distributedOnly, [products]),
                "read tagged cache entry before invalidation");
            const string tagMarkerKey = "__MSFT_HCT__gma:redistests:catalog:global:global:products";
            byte[]? tagMarkerBefore = await WithTimeout(
                configuredDistributedCache.GetAsync(tagMarkerKey),
                "read tag marker before invalidation");
            await Task.Delay(100);
            firstQueue.RemoveByTag(products);
            await WithTimeout(
                SendSuccessfulCommandAsync(firstScope.ServiceProvider),
                "flush tag invalidation");
            await WaitForDistributedChangeAsync(
                configuredDistributedCache,
                tagMarkerKey,
                tagMarkerBefore);
            await WithTimeout(
                first.GetOrCreateAsync(taggedKey, _ => ValueTask.FromResult(++tagCalls), distributedOnly, [products]),
                "read tagged cache entry after invalidation");
            Assert.Equal(2, tagCalls);
        }
        finally
        {
            await redis.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Environment.EnvironmentName = "RedisTests";
        builder.Configuration["Caching:Enabled"] = "true";
        builder.Configuration["Caching:Provider"] = "Redis";
        builder.Configuration["Caching:DefaultDistributedExpiration"] = "00:05:00";
        builder.Configuration["Caching:DefaultLocalExpiration"] = "00:00:30";
        builder.Configuration["ConnectionStrings:redis"] = connectionString;
        builder.Configuration["Tenancy:Enabled"] = "false";
        builder.AddRedisCaching();
        builder.AddCachingCqrs();
        builder.Services.AddScoped<ICommandHandler<FlushInvalidationsCommand, Unit>, FlushInvalidationsHandler>();
        return builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task SendSuccessfulCommandAsync(IServiceProvider provider)
    {
        IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
        await dispatcher.SendAsync(new FlushInvalidationsCommand());
    }

    private static async Task WaitForDistributedValueAsync(IDistributedCache cache, string key)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        while (await cache.GetAsync(key, timeout.Token) is null)
        {
            await Task.Delay(25, timeout.Token);
        }
    }

    private static async Task<T> WithTimeout<T>(ValueTask<T> operation, string operationName)
    {
        try
        {
            return await operation.AsTask().WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException($"{operationName} timed out.", exception);
        }
    }

    private static async Task<T> WithTimeout<T>(Task<T> operation, string operationName)
    {
        try
        {
            return await operation.WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException($"{operationName} timed out.", exception);
        }
    }

    private static async Task WithTimeout(Task operation, string operationName)
    {
        try
        {
            await operation.WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException($"{operationName} timed out.", exception);
        }
    }

    private static async Task WaitForTcpPortAsync(int port)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        Exception? lastException = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using TcpClient client = new();
                await client.ConnectAsync(IPAddress.Loopback, port).WaitAsync(TimeSpan.FromSeconds(1));
                return;
            }
            catch (Exception exception) when (exception is SocketException or TimeoutException)
            {
                lastException = exception;
                await Task.Delay(100);
            }
        }

        throw new TimeoutException($"Redis did not become reachable on mapped port {port}.", lastException);
    }
    private static async Task WaitForDistributedChangeAsync(
        IDistributedCache cache,
        string key,
        byte[]? previousValue)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

        while (true)
        {
            byte[]? currentValue = await cache.GetAsync(key, timeout.Token);

            if (currentValue is not null &&
                (previousValue is null || !currentValue.AsSpan().SequenceEqual(previousValue)))
            {
                return;
            }

            await Task.Delay(25, timeout.Token);
        }
    }

    private sealed record FlushInvalidationsCommand : ICommand<Unit>;

    private sealed class FlushInvalidationsHandler : ICommandHandler<FlushInvalidationsCommand, Unit>
    {
        public Task<Result<Unit>> HandleAsync(
            FlushInvalidationsCommand command,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(Unit.Value));
    }
}
