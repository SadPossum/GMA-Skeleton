namespace Gma.Modules.Notifications.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Gma.Modules.Notifications.Application;
using Xunit;

[Trait("Category", "Unit")]
public sealed class NotificationsApplicationRegistrationTests
{
    [Fact]
    public void Application_registration_provides_default_durable_stream_options()
    {
        ServiceCollection services = new();

        services.AddNotificationsApplication();

        using ServiceProvider provider = services.BuildServiceProvider();
        NotificationStreamOptions options = provider
            .GetRequiredService<IOptions<NotificationStreamOptions>>()
            .Value;

        Assert.Equal(NotificationStreamOptions.DefaultBatchSize, options.BatchSize);
        Assert.Equal(NotificationStreamOptions.DefaultPollInterval, options.PollInterval);
    }

    [Fact]
    public void Application_registration_rejects_invalid_durable_stream_options()
    {
        ConfigurationBuilder configurationBuilder = new();
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notifications:DurableStreams:BatchSize"] = "0"
        });
        IConfiguration configuration = configurationBuilder.Build();
        ServiceCollection services = new();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(
            () => services.AddNotificationsApplication(configuration));

        Assert.Contains("BatchSize", exception.Message, StringComparison.Ordinal);
    }
}
