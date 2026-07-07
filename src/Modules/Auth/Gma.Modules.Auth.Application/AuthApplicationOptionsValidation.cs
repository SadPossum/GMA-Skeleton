namespace Gma.Modules.Auth.Application;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

internal static class AuthApplicationOptionsValidation
{
    public static AuthApplicationOptions GetValidatedOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        AuthApplicationOptions options = configuration
            .GetSection(AuthApplicationOptions.SectionName)
            .Get<AuthApplicationOptions>() ?? new AuthApplicationOptions();

        ValidateOptionsResult result = new AuthApplicationOptionsValidator().Validate(name: null, options);

        if (result.Failed)
        {
            throw new OptionsValidationException(
                AuthApplicationOptions.SectionName,
                typeof(AuthApplicationOptions),
                result.Failures);
        }

        return options;
    }
}
