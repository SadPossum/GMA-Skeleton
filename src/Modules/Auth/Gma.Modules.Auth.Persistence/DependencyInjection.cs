namespace Gma.Modules.Auth.Persistence;

using Gma.Modules.Auth.Application.Ports;
using Gma.Modules.Auth.Domain.Repositories;
using Gma.Modules.Auth.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Messaging;
using Gma.Framework.Cqrs.UnitOfWork;
using Gma.Framework.Persistence.EntityFrameworkCore;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddAuthPersistence(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddPersistenceOptions(builder.Configuration);

        builder.Services.TryAddModuleDbContext<AuthDbContext>(options =>
            options.UseConfiguredProvider(
                builder.Configuration,
                AuthMigrations.SqlServerAssembly,
                AuthMigrations.PostgreSqlAssembly,
                AuthMigrations.Schema,
                AuthMigrations.HistoryTable));

        builder.Services.TryAddScoped<IMemberRepository, MemberRepository>();
        builder.Services.TryAddScoped<IAdminMemberReadRepository, AdminMemberReadRepository>();
        builder.Services.TryAddEnumerable([
            ServiceDescriptor.Scoped<IUnitOfWork, AuthUnitOfWork>(),
            ServiceDescriptor.Scoped<IOutboxWriter, AuthOutboxWriter>(),
            ServiceDescriptor.Scoped<IOutboxStore, AuthOutboxStore>()
        ]);

        return builder;
    }
}
