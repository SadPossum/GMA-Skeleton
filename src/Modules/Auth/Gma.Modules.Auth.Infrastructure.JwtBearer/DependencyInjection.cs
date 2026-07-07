namespace Gma.Modules.Auth.Infrastructure.JwtBearer;

using Gma.Modules.Auth.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddAuthJwtBearerAuthentication(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddAuthInfrastructure(builder.Configuration);

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(AuthJwtBearerRegistrationMarker)))
        {
            return builder;
        }

        builder.Services.AddSingleton<AuthJwtBearerRegistrationMarker>();
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        builder.Services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, jwtOptions) =>
            {
                options.TokenValidationParameters = JwtTokenValidationParametersFactory.Create(
                    jwtOptions.Value,
                    validateLifetime: true);
            });

        return builder;
    }

    private sealed class AuthJwtBearerRegistrationMarker;
}
