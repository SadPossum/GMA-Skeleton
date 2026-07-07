namespace Gma.Modules.Administration.AdminCli;

using System.CommandLine;
using Gma.Modules.Administration.Application;
using Gma.Modules.Administration.Application.Commands;
using Gma.Modules.Administration.Application.Queries;
using Gma.Modules.Administration.Contracts;
using Gma.Modules.Administration.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;

public sealed class AdministrationAdminCliModule : IAdminCliModule
{
    private const string AuditFailureMessage = "Admin audit failed.";

    public string Name => AdministrationModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddAdministrationApplication(builder.Configuration);
        builder.AddAdministrationPersistence();
    }

    public void MapCommands(IAdminCliCommandRegistry commands)
    {
        AdminCliGlobalOptions globalOptions = commands.Services.GetRequiredService<AdminCliGlobalOptions>();
        Command roles = new("roles", "Manage admin roles.")
        {
            CreateRoleCreateCommand(commands.Services),
            CreateRoleGrantCommand(commands.Services),
            CreateRoleAssignCommand(commands.Services, globalOptions),
            CreateRoleListCommand(commands.Services, globalOptions)
        };
        Command admin = new(AdministrationModuleMetadata.AdminSurfaceName, "Administration operations.")
        {
            CreateBootstrapCommand(commands.Services, globalOptions),
            roles
        };

        commands.AddCommand(this.Name, admin);
    }

    private static Command CreateBootstrapCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<bool> yesOption = new("--yes")
        {
            Description = "Confirm first-owner bootstrap."
        };
        Command command = new("bootstrap", "Bootstrap the first owner principal.")
        {
            yesOption
        };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            string? actorId = parseResult.GetValue(globalOptions.ActorOption);

            if (string.IsNullOrWhiteSpace(actorId))
            {
                AdminCliOutput.WriteError("--actor is required for admin bootstrap.");
                return AdminExitCodes.ValidationFailed;
            }

            if (!AdminActor.TrySystem(actorId, out AdminActor? actor))
            {
                AdminCliOutput.WriteError(AdminActor.InvalidIdMessage);
                return AdminExitCodes.ValidationFailed;
            }

            using IServiceScope scope = services.CreateScope();
            IServiceProvider provider = scope.ServiceProvider;
            IAdminActorContextAccessor actorContext = provider.GetRequiredService<IAdminActorContextAccessor>();
            IAdminAuditSink auditSink = provider.GetRequiredService<IAdminAuditSink>();
            ISystemClock clock = provider.GetRequiredService<ISystemClock>();
            IIdGenerator idGenerator = provider.GetRequiredService<IIdGenerator>();
            IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
            AdminOperation operation = AdminOperation.Create(
                AdministrationAdminOperationNames.Bootstrap,
                AdministrationPermissions.Bootstrap);

            actorContext.SetActor(actor);
            Result<Unit> result = await dispatcher
                .SendAsync(new BootstrapOwnerCommand(actor.Id, parseResult.GetValue(yesOption)), cancellationToken)
                .ConfigureAwait(false);

            await RecordBootstrapAuditAsync(auditSink, clock, idGenerator, actor, operation, result, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsFailure)
            {
                AdminCliOutput.WriteError(result.Error.Message);
                return AdminExitCodes.Failed;
            }

            AdminCliOutput.WriteMessage($"Bootstrapped owner principal '{actor.Id}'.");
            return AdminExitCodes.Success;
        });

        return command;
    }

    private static Command CreateRoleCreateCommand(IServiceProvider services)
    {
        Option<string> nameOption = new("--name")
        {
            Description = "Role name.",
            Required = true
        };
        Command command = new("create", "Create an admin role.")
        {
            nameOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string name = parseResult.GetRequiredValue(nameOption);

            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(AdministrationAdminOperationNames.RolesCreate, AdministrationPermissions.RolesManage),
                null,
                requireTenant: false,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<AdminRoleDetails> result = await dispatcher.SendAsync(new CreateRoleCommand(name), token)
                        .ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage($"Created role '{result.Value.Name}'.");
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateRoleGrantCommand(IServiceProvider services)
    {
        Option<string> roleOption = new("--role")
        {
            Description = "Role name.",
            Required = true
        };
        Option<string> permissionOption = new("--permission")
        {
            Description = "Permission code to grant.",
            Required = true
        };
        Command command = new("grant", "Grant a permission to a role.")
        {
            roleOption,
            permissionOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();

            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(AdministrationAdminOperationNames.RolesGrant, AdministrationPermissions.RolesManage),
                null,
                requireTenant: false,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<Unit> result = await dispatcher.SendAsync(
                        new GrantRolePermissionCommand(
                            parseResult.GetRequiredValue(roleOption),
                            parseResult.GetRequiredValue(permissionOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage("Permission granted.");
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateRoleAssignCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<string> targetActorOption = new("--target-actor")
        {
            Description = "Principal that receives the role.",
            Required = true
        };
        Option<string> roleOption = new("--role")
        {
            Description = "Role name.",
            Required = true
        };
        Command command = new("assign", "Assign a role to an admin principal.")
        {
            targetActorOption,
            roleOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string? tenantId = parseResult.GetValue(globalOptions.TenantOption);

            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(AdministrationAdminOperationNames.RolesAssign, AdministrationPermissions.RolesManage),
                tenantId,
                requireTenant: false,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<Unit> result = await dispatcher.SendAsync(
                        new AssignRoleCommand(
                            parseResult.GetRequiredValue(targetActorOption),
                            parseResult.GetRequiredValue(roleOption),
                            tenantId),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage("Role assigned.");
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateRoleListCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Command command = new("list", "List admin roles.");
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();

            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(AdministrationAdminOperationNames.RolesList, AdministrationPermissions.RolesRead),
                null,
                requireTenant: false,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<IReadOnlyList<AdminRoleDetails>> result = await dispatcher.QueryAsync(new ListRolesQuery(), token)
                        .ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteRows(
                            result.Value,
                            parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table,
                            [
                                ("Name", role => role.Name),
                                ("Permissions", role => string.Join(",", role.Permissions)),
                                ("Assignments", role => role.AssignmentCount.ToString(System.Globalization.CultureInfo.InvariantCulture))
                            ]);
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static async Task RecordBootstrapAuditAsync(
        IAdminAuditSink auditSink,
        ISystemClock clock,
        IIdGenerator idGenerator,
        AdminActor actor,
        AdminOperation operation,
        Result result,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditSink.RecordAsync(
                new AdminAuditRecord(
                    idGenerator.NewId(),
                    actor.Id,
                    null,
                    operation.Name,
                    operation.Permission.Code,
                    result.IsSuccess ? AdminAuditResult.Succeeded : AdminAuditResult.Failed,
                    result.IsSuccess ? null : result.Error.Code,
                    clock.UtcNow),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AdminCliOutput.WriteError(AuditFailureMessage);
        }
    }
}
