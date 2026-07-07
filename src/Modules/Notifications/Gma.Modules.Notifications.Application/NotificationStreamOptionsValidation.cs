namespace Gma.Modules.Notifications.Application;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

internal static class NotificationStreamOptionsValidation
{
    public static NotificationStreamOptions GetValidatedOptions(IConfiguration configuration)
    {
        NotificationStreamOptions options = new();
        configuration.GetSection(NotificationStreamOptions.SectionName).Bind(options);

        ValidateOptionsResult result = new NotificationStreamOptionsValidator().Validate(name: null, options);
        if (result.Failed)
        {
            throw new OptionsValidationException(
                NotificationStreamOptions.SectionName,
                typeof(NotificationStreamOptions),
                result.Failures);
        }

        return options;
    }
}
