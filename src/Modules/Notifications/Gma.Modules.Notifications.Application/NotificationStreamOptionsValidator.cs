namespace Gma.Modules.Notifications.Application;

using Microsoft.Extensions.Options;

internal sealed class NotificationStreamOptionsValidator : IValidateOptions<NotificationStreamOptions>
{
    public ValidateOptionsResult Validate(string? name, NotificationStreamOptions options)
    {
        if (options.BatchSize is <= 0 or > NotificationStreamOptions.MaxBatchSize)
        {
            return ValidateOptionsResult.Fail(
                $"{NotificationStreamOptions.SectionName}:BatchSize must be between 1 and {NotificationStreamOptions.MaxBatchSize}.");
        }

        if (options.PollInterval < TimeSpan.FromMilliseconds(250) ||
            options.PollInterval > TimeSpan.FromMinutes(1))
        {
            return ValidateOptionsResult.Fail(
                $"{NotificationStreamOptions.SectionName}:PollInterval must be between 250 milliseconds and 1 minute.");
        }

        return ValidateOptionsResult.Success;
    }
}
