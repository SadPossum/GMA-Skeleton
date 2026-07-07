namespace Gma.Modules.Administration.Application;

using Microsoft.Extensions.Options;

internal sealed class AdministrationOptionsValidator : IValidateOptions<AdministrationOptions>
{
    public ValidateOptionsResult Validate(string? name, AdministrationOptions options)
    {
        if (!AdminRoleName.TryNormalize(options.Bootstrap.OwnerRoleName, out string? normalizedOwnerRoleName) ||
            !string.Equals(options.Bootstrap.OwnerRoleName, normalizedOwnerRoleName, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                $"{AdministrationOptions.SectionName}:Bootstrap:OwnerRoleName must be a lowercase role slug.");
        }

        return ValidateOptionsResult.Success;
    }
}
