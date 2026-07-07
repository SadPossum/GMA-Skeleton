namespace Gma.Modules.Auth.Infrastructure;

using System.Text;
using Microsoft.Extensions.Options;

internal sealed class RefreshTokenHashingOptionsValidator : IValidateOptions<RefreshTokenHashingOptions>
{
    public ValidateOptionsResult Validate(string? name, RefreshTokenHashingOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Pepper))
        {
            return ValidateOptionsResult.Fail($"{RefreshTokenHashingOptions.SectionName}:Pepper is required.");
        }

        if (Encoding.UTF8.GetByteCount(options.Pepper) < RefreshTokenHashingOptions.MinimumPepperBytes)
        {
            return ValidateOptionsResult.Fail(
                $"{RefreshTokenHashingOptions.SectionName}:Pepper must be at least " +
                $"{RefreshTokenHashingOptions.MinimumPepperBytes} bytes.");
        }

        return ValidateOptionsResult.Success;
    }
}
