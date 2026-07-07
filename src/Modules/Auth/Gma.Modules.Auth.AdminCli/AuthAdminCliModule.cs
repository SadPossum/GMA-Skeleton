namespace Gma.Modules.Auth.AdminCli;

using System.CommandLine;
using Gma.Modules.Auth.Admin.Contracts;
using Gma.Modules.Auth.Application;
using Gma.Modules.Auth.Application.Commands;
using Gma.Modules.Auth.Application.Queries;
using Gma.Modules.Auth.Application.Security;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Auth.Infrastructure;
using Gma.Modules.Auth.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Tenancy;
using Gma.Framework.Results;

public sealed class AuthAdminCliModule(AuthProfile profile) : IAdminCliModule
{
    private readonly AuthProfile profile = profile ?? throw new ArgumentNullException(nameof(profile));

    public AuthAdminCliModule()
        : this(AuthProfile.TenantScoped())
    {
    }

    public string Name => AuthModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        AddProfileServices(builder, this.profile);
        builder.Services.AddAuthApplication(builder.Configuration);
        builder.Services.AddAuthInfrastructure(builder.Configuration);
        builder.AddAuthPersistence();
    }

    public void MapCommands(IAdminCliCommandRegistry commands)
    {
        AdminCliGlobalOptions globalOptions = commands.Services.GetRequiredService<AdminCliGlobalOptions>();
        bool requireTenant = this.profile.RequiresTenantContext;
        Command members = new("members", "Manage Auth members.")
        {
            CreateListCommand(commands.Services, globalOptions, requireTenant),
            CreateGetCommand(commands.Services, globalOptions, requireTenant),
            CreateCreateCommand(commands.Services, globalOptions, requireTenant),
            CreateDisableCommand(commands.Services, globalOptions, requireTenant),
            CreateEnableCommand(commands.Services, globalOptions, requireTenant),
            CreateResetPasswordCommand(commands.Services, globalOptions, requireTenant),
            CreateRevokeSessionsCommand(commands.Services, globalOptions, requireTenant)
        };
        Command auth = new(AuthModuleMetadata.Name, "Auth administration operations.")
        {
            members
        };

        commands.AddCommand(this.Name, auth);
    }

    private static void AddProfileServices(IHostApplicationBuilder builder, AuthProfile profile)
    {
        builder.SelectModuleProfile(profile.Descriptor, "Gma.Modules.Auth.AdminCli");

        if (!profile.RequiresTenantContext &&
            !string.IsNullOrWhiteSpace(profile.GlobalScopeId))
        {
            builder.Services.PostConfigure<TenantOptions>(options => options.LocalDefaultTenantId = profile.GlobalScopeId);
        }
    }

    private static Command CreateListCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions, bool requireTenant)
    {
        Option<int> pageOption = new("--page")
        {
            Description = "Page number.",
            DefaultValueFactory = _ => PageRequest.DefaultPage
        };
        Option<int> pageSizeOption = new("--page-size")
        {
            Description = "Page size.",
            DefaultValueFactory = _ => PageRequest.DefaultPageSize
        };
        Command command = new("list", "List members.")
        {
            pageOption,
            pageSizeOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string? tenantId = parseResult.GetValue(globalOptions.TenantOption);

            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(AuthAdminOperationNames.MembersList, AuthAdminPermissions.MembersRead),
                tenantId,
                requireTenant,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<AdminMemberListResponse> result = await dispatcher.QueryAsync(
                        new ListAdminMembersQuery(parseResult.GetValue(pageOption), parseResult.GetValue(pageSizeOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteRows(
                            result.Value.Items,
                            parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table,
                            [
                                ("MemberId", item => item.MemberId.ToString()),
                                ("Username", item => item.ActiveUsername ?? string.Empty),
                                ("Status", item => MemberStatusNames.ToWireName(item.Status)),
                                ("Sessions", item => item.ActiveSessionCount.ToString(System.Globalization.CultureInfo.InvariantCulture))
                            ]);
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateGetCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions, bool requireTenant)
    {
        Option<Guid> memberIdOption = CreateMemberIdOption();
        Command command = new("get", "Get member details.")
        {
            memberIdOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string? tenantId = parseResult.GetValue(globalOptions.TenantOption);

            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(AuthAdminOperationNames.MembersGet, AuthAdminPermissions.MembersRead),
                tenantId,
                requireTenant,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<AdminMemberDetails> result = await dispatcher.QueryAsync(
                        new GetAdminMemberQuery(parseResult.GetRequiredValue(memberIdOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteRows(
                            [result.Value],
                            parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table,
                            [
                                ("MemberId", item => item.MemberId.ToString()),
                                ("Username", item => item.ActiveUsername ?? string.Empty),
                                ("Status", item => MemberStatusNames.ToWireName(item.Status)),
                                ("ActiveSessions", item => item.ActiveSessionCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                                ("RegisteredAtUtc", item => item.RegisteredAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture))
                            ]);
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateCreateCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions, bool requireTenant)
    {
        Option<string> usernameOption = new("--username")
        {
            Description = "Member username.",
            Required = true
        };
        Option<string> usernameTypeOption = new("--username-type")
        {
            Description = "Username type: email or phone.",
            DefaultValueFactory = _ => "email"
        };
        Option<bool> generatePasswordOption = new("--generate-password")
        {
            Description = "Generate a password and print it once after success."
        };
        Option<bool> passwordStdinOption = new("--password-stdin")
        {
            Description = "Read the password from standard input."
        };
        Command command = new("create", "Create a member.")
        {
            usernameOption,
            usernameTypeOption,
            generatePasswordOption,
            passwordStdinOption
        };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string? tenantId = parseResult.GetValue(globalOptions.TenantOption);

            return await executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(AuthAdminOperationNames.MembersCreate, AuthAdminPermissions.MembersCreate),
                tenantId,
                requireTenant,
                async (provider, token) =>
                {
                    Result<UsernameType> usernameType = ParseUsernameType(parseResult.GetValue(usernameTypeOption) ?? "email");
                    if (usernameType.IsFailure)
                    {
                        return Result.Failure<AdminCreatedMemberResponse>(usernameType.Error);
                    }

                    Result<PasswordInput> passwordInput = await ReadPasswordAsync(
                            parseResult.GetValue(generatePasswordOption),
                            parseResult.GetValue(passwordStdinOption),
                            token)
                        .ConfigureAwait(false);

                    if (passwordInput.IsFailure)
                    {
                        return Result.Failure<AdminCreatedMemberResponse>(passwordInput.Error);
                    }

                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<AdminCreatedMemberResponse> result = await dispatcher.SendAsync(
                        new AdminCreateMemberCommand(
                            parseResult.GetRequiredValue(usernameOption),
                            usernameType.Value,
                            passwordInput.Value.Password),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage($"Created member '{result.Value.MemberId}' ({result.Value.Username}).");

                        if (passwordInput.Value.Generated)
                        {
                            AdminCliOutput.WriteMessage($"Generated password: {passwordInput.Value.Password}");
                        }
                    }

                    return result;
                },
                cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private static Command CreateDisableCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions, bool requireTenant)
    {
        Option<Guid> memberIdOption = CreateMemberIdOption();
        Option<string> reasonOption = new("--reason")
        {
            Description = "Disable reason.",
            Required = true
        };
        Option<bool> yesOption = CreateYesOption();
        Command command = new("disable", "Disable a member and revoke active sessions.")
        {
            memberIdOption,
            reasonOption,
            yesOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string? tenantId = parseResult.GetValue(globalOptions.TenantOption);

            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(AuthAdminOperationNames.MembersDisable, AuthAdminPermissions.MembersDisable),
                tenantId,
                requireTenant,
                async (provider, token) =>
                {
                    if (!parseResult.GetValue(yesOption))
                    {
                        return Result.Failure<Unit>(AdminErrors.ConfirmationRequired);
                    }

                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<Unit> result = await dispatcher.SendAsync(
                        new DisableMemberCommand(parseResult.GetRequiredValue(memberIdOption), parseResult.GetRequiredValue(reasonOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage("Member disabled.");
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateEnableCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions, bool requireTenant)
    {
        Option<Guid> memberIdOption = CreateMemberIdOption();
        Command command = new("enable", "Enable a disabled member.")
        {
            memberIdOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string? tenantId = parseResult.GetValue(globalOptions.TenantOption);

            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(AuthAdminOperationNames.MembersEnable, AuthAdminPermissions.MembersEnable),
                tenantId,
                requireTenant,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<Unit> result = await dispatcher.SendAsync(new EnableMemberCommand(parseResult.GetRequiredValue(memberIdOption)), token)
                        .ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage("Member enabled.");
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateResetPasswordCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions, bool requireTenant)
    {
        Option<Guid> memberIdOption = CreateMemberIdOption();
        Option<bool> generatePasswordOption = new("--generate-password")
        {
            Description = "Generate a password and print it once after success."
        };
        Option<bool> passwordStdinOption = new("--password-stdin")
        {
            Description = "Read the new password from standard input."
        };
        Command command = new("reset-password", "Reset a member password.")
        {
            memberIdOption,
            generatePasswordOption,
            passwordStdinOption
        };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string? tenantId = parseResult.GetValue(globalOptions.TenantOption);

            return await executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(AuthAdminOperationNames.MembersResetPassword, AuthAdminPermissions.MembersResetPassword),
                tenantId,
                requireTenant,
                async (provider, token) =>
                {
                    Result<PasswordInput> passwordInput = await ReadPasswordAsync(
                            parseResult.GetValue(generatePasswordOption),
                            parseResult.GetValue(passwordStdinOption),
                            token)
                        .ConfigureAwait(false);

                    if (passwordInput.IsFailure)
                    {
                        return Result.Failure<Unit>(passwordInput.Error);
                    }

                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<Unit> result = await dispatcher.SendAsync(
                        new ResetMemberPasswordCommand(parseResult.GetRequiredValue(memberIdOption), passwordInput.Value.Password),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage("Password reset.");

                        if (passwordInput.Value.Generated)
                        {
                            AdminCliOutput.WriteMessage($"Generated password: {passwordInput.Value.Password}");
                        }
                    }

                    return result;
                },
                cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private static Command CreateRevokeSessionsCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions, bool requireTenant)
    {
        Option<Guid> memberIdOption = CreateMemberIdOption();
        Option<bool> yesOption = CreateYesOption();
        Command command = new("revoke-sessions", "Revoke all active sessions for a member.")
        {
            memberIdOption,
            yesOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string? tenantId = parseResult.GetValue(globalOptions.TenantOption);

            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(AuthAdminOperationNames.MembersRevokeSessions, AuthAdminPermissions.MembersRevokeSessions),
                tenantId,
                requireTenant,
                async (provider, token) =>
                {
                    if (!parseResult.GetValue(yesOption))
                    {
                        return Result.Failure<AdminRevokeSessionsResponse>(AdminErrors.ConfirmationRequired);
                    }

                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<AdminRevokeSessionsResponse> result = await dispatcher.SendAsync(
                        new RevokeMemberSessionsCommand(parseResult.GetRequiredValue(memberIdOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage($"Revoked {result.Value.RevokedSessionCount} active session(s).");
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Option<Guid> CreateMemberIdOption() =>
        new("--member-id")
        {
            Description = "Member id.",
            Required = true
        };

    private static Option<bool> CreateYesOption() =>
        new("--yes")
        {
            Description = "Confirm this destructive operation."
        };

    private static Result<UsernameType> ParseUsernameType(string value)
    {
        if (string.Equals(value, "email", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success(UsernameType.Email);
        }

        if (string.Equals(value, "phone", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Success(UsernameType.Phone);
        }

        return Result.Failure<UsernameType>(AuthApplicationErrors.UsernameTypeInvalid);
    }

    private static async Task<Result<PasswordInput>> ReadPasswordAsync(
        bool generatePassword,
        bool passwordStdin,
        CancellationToken cancellationToken)
    {
        if (generatePassword && passwordStdin)
        {
            return Result.Failure<PasswordInput>(AdminCliErrors.PasswordSourceConflict);
        }

        if (generatePassword)
        {
            return Result.Success(new PasswordInput(AdminPasswordGenerator.Generate(), Generated: true));
        }

        if (passwordStdin)
        {
            string? password = await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            Result<string> requiredPassword = RequirePassword(password);
            return requiredPassword.IsFailure
                ? Result.Failure<PasswordInput>(requiredPassword.Error)
                : Result.Success(new PasswordInput(requiredPassword.Value, Generated: false));
        }

        Result<string> hiddenPassword = ReadHiddenPassword();
        return hiddenPassword.IsFailure
            ? Result.Failure<PasswordInput>(hiddenPassword.Error)
            : Result.Success(new PasswordInput(hiddenPassword.Value, Generated: false));
    }

    private static Result<string> ReadHiddenPassword()
    {
        AdminCliOutput.WriteErrorInline("Password: ");
        List<char> password = [];

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                AdminCliOutput.WriteErrorLine();
                return RequirePassword(new string([.. password]));
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Count > 0)
                {
                    password.RemoveAt(password.Count - 1);
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.Add(key.KeyChar);
            }
        }
    }

    private static Result<string> RequirePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return Result.Failure<string>(AdminCliErrors.PasswordRequired);
        }

        return Result.Success(password);
    }

    private sealed record PasswordInput(string Password, bool Generated);

    private static class AdminCliErrors
    {
        public static readonly Error PasswordRequired = new("Admin.PasswordRequired", "A password is required unless password generation is requested.");
        public static readonly Error PasswordSourceConflict = new("Admin.PasswordSourceConflict", "Use either --generate-password or --password-stdin, not both.");
    }
}

public static class AuthAdminCliModuleHostBuilderExtensions
{
    public static IHostApplicationBuilder AddAuthAdminModule(this IHostApplicationBuilder builder, AuthProfile profile)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(profile);

        return builder.AddAdminModule(new AuthAdminCliModule(profile));
    }
}
