namespace ServiceDefaults.Tests;

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OtlpHttpClientTests
{
    [Fact]
    public async Task Exporter_clients_deliver_direct_requests_without_forwarding_redirects_or_headers()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
        int receivedAtTarget = 0;
        int forwardedCanaries = 0;
        await using WebApplication target = CreateCollector();
        target.MapGet("/receive", (HttpContext context) =>
        {
            Interlocked.Increment(ref receivedAtTarget);
            if (context.Request.Headers.ContainsKey("X-Prototype-Canary"))
            {
                Interlocked.Increment(ref forwardedCanaries);
            }

            return Results.Ok();
        });
        await target.StartAsync(timeout.Token);

        int directRequests = 0;
        await using WebApplication collector = CreateCollector();
        collector.MapGet("/direct", () =>
        {
            Interlocked.Increment(ref directRequests);
            return Results.Ok();
        });
        collector.MapGet("/redirect", () =>
            Results.Redirect($"{target.Urls.Single()}/receive", permanent: false, preserveMethod: true));
        await collector.StartAsync(timeout.Token);

        HostApplicationBuilder builder = CreateHostBuilder(otlpEnabled: true);
        builder.AddServiceDefaults();
        using ServiceProvider services = builder.Services.BuildServiceProvider();
        IHttpClientFactory factory = services.GetRequiredService<IHttpClientFactory>();

        foreach (string name in new[] { "OtlpTraceExporter", "OtlpMetricExporter", "OtlpLogExporter" })
        {
            using HttpClient client = factory.CreateClient(name);
            using HttpResponseMessage direct = await client.GetAsync($"{collector.Urls.Single()}/direct", timeout.Token);
            Assert.Equal(HttpStatusCode.OK, direct.StatusCode);

            using HttpRequestMessage request = new(HttpMethod.Get, $"{collector.Urls.Single()}/redirect");
            request.Headers.Add("X-Prototype-Canary", "fixture-not-a-secret");
            using HttpResponseMessage redirected = await client.SendAsync(request, timeout.Token);
            Assert.Equal(HttpStatusCode.TemporaryRedirect, redirected.StatusCode);
        }

        Assert.Equal(3, directRequests);
        Assert.Equal(0, receivedAtTarget);
        Assert.Equal(0, forwardedCanaries);

        using HttpClient ordinaryClient = factory.CreateClient("ordinary-business-client");
        using HttpResponseMessage ordinaryResponse = await ordinaryClient.GetAsync(
            $"{collector.Urls.Single()}/redirect", timeout.Token);
        Assert.Equal(HttpStatusCode.OK, ordinaryResponse.StatusCode);
        Assert.Equal(1, receivedAtTarget);
        Assert.Equal(0, forwardedCanaries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exporter_policy_preserves_existing_standard_primary_handler_settings(bool useSocketsHandler)
    {
        HostApplicationBuilder builder = CreateHostBuilder(otlpEnabled: true);
        using HttpMessageHandler handler = useSocketsHandler
            ? new SocketsHttpHandler { UseProxy = false, MaxConnectionsPerServer = 7 }
            : new HttpClientHandler { UseProxy = false, MaxConnectionsPerServer = 7 };
        builder.Services.AddHttpClient("OtlpTraceExporter").ConfigurePrimaryHttpMessageHandler(() => handler);
        builder.AddServiceDefaults();
        using ServiceProvider services = builder.Services.BuildServiceProvider();
        services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler("OtlpTraceExporter");

        if (handler is SocketsHttpHandler sockets)
        {
            Assert.False(sockets.AllowAutoRedirect);
            Assert.False(sockets.UseProxy);
            Assert.Equal(7, sockets.MaxConnectionsPerServer);
        }
        else
        {
            HttpClientHandler client = Assert.IsType<HttpClientHandler>(handler);
            Assert.False(client.AllowAutoRedirect);
            Assert.False(client.UseProxy);
            Assert.Equal(7, client.MaxConnectionsPerServer);
        }
    }

    [Fact]
    public void Disabled_export_does_not_change_a_preconfigured_client()
    {
        HostApplicationBuilder builder = CreateHostBuilder(otlpEnabled: false);
        using SocketsHttpHandler handler = new();
        builder.Services.AddHttpClient("OtlpTraceExporter").ConfigurePrimaryHttpMessageHandler(() => handler);
        builder.AddServiceDefaults();
        using ServiceProvider services = builder.Services.BuildServiceProvider();
        services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler("OtlpTraceExporter");

        Assert.True(handler.AllowAutoRedirect);
    }

    private static HostApplicationBuilder CreateHostBuilder(bool otlpEnabled)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration["Observability:Otlp:Enabled"] = otlpEnabled ? "true" : "false";
        return builder;
    }

    private static WebApplication CreateCollector()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        return builder.Build();
    }
}
