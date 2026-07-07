namespace Gma.Modules.Administration.Application;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

internal static class AdministrationOptionsValidation
{
    public static AdministrationOptions GetValidatedOptions(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        AdministrationOptions options = configuration
            .GetSection(AdministrationOptions.SectionName)
            .Get<AdministrationOptions>() ?? new AdministrationOptions();

        ValidateOptionsResult result = new AdministrationOptionsValidator().Validate(name: null, options);

        if (result.Failed)
        {
            throw new OptionsValidationException(
                AdministrationOptions.SectionName,
                typeof(AdministrationOptions),
                result.Failures);
        }

        return options;
    }
}
