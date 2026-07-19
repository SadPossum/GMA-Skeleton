namespace Host.Api;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal static class DataProtectionComposition
{
    public static IHostApplicationBuilder AddConfiguredDataProtection(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        string? configuredApplicationName = builder.Configuration["DataProtection:ApplicationName"];
        string? applicationNamespace = builder.Configuration["ApplicationIdentity:Namespace"];
        string applicationName = !string.IsNullOrWhiteSpace(configuredApplicationName)
            ? configuredApplicationName.Trim()
            : applicationNamespace?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            throw new InvalidOperationException(
                "DataProtection:ApplicationName or ApplicationIdentity:Namespace must provide a stable application name.");
        }

        IDataProtectionBuilder dataProtection = builder.Services
            .AddDataProtection()
            .SetApplicationName(applicationName);
        string? configuredKeyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
        if (string.IsNullOrWhiteSpace(configuredKeyRingPath))
        {
            if (builder.Environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "DataProtection:KeyRingPath is required in Production so OIDC state and protected Auth secrets survive restarts and work across replicas.");
            }

            return builder;
        }

        string keyRingPath = Path.GetFullPath(
            configuredKeyRingPath.Trim(),
            builder.Environment.ContentRootPath);
        dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));
        return builder;
    }
}
