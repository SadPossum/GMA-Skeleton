namespace Gma.Modules.TaskRuntime.AdminCli;

using System.CommandLine;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Tasks;
using Gma.Framework.Results;
using Gma.Modules.TaskRuntime.Admin.Contracts;
using Gma.Modules.TaskRuntime.Application;
using Gma.Modules.TaskRuntime.Application.Commands;
using Gma.Modules.TaskRuntime.Application.Queries;
using Gma.Modules.TaskRuntime.Contracts;
using Gma.Modules.TaskRuntime.Persistence;

public sealed class TaskRuntimeAdminCliModule : IAdminCliModule
{
    public string Name => TaskRuntimeModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(TaskRuntimeProfiles.Default, "Gma.Modules.TaskRuntime.AdminCli");
        builder.Services.AddTaskRuntimeApplication();
        builder.AddTaskRuntimePersistence();
    }

    public void MapCommands(IAdminCliCommandRegistry commands)
    {
        AdminCliGlobalOptions globalOptions = commands.Services.GetRequiredService<AdminCliGlobalOptions>();
        Command runs = new("runs", "Manage task runs.")
        {
            CreateListCommand(commands.Services, globalOptions),
            CreateStatsCommand(commands.Services, globalOptions),
            CreateGetCommand(commands.Services, globalOptions),
            CreateEnqueueCommand(commands.Services, globalOptions),
            CreateControlCommand(commands.Services, globalOptions),
            CreateCancelCommand(commands.Services, globalOptions),
            CreateRetryCommand(commands.Services, globalOptions)
        };
        Command tasks = new(TaskRuntimeModuleMetadata.AdminSurfaceName, "Task runtime administration operations.")
        {
            runs
        };

        commands.AddCommand(this.Name, tasks);
    }

    private static Command CreateListCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<string?> moduleOption = new("--module") { Description = "Filter by module name." };
        Option<string?> taskOption = new("--task") { Description = "Filter by task name." };
        Option<string?> workerGroupOption = new("--worker-group") { Description = "Filter by worker group." };
        Option<string?> statusOption = new("--status") { Description = "Filter by task run status, e.g. retry-scheduled." };
        Option<int> pageOption = new("--page") { DefaultValueFactory = _ => PageRequest.DefaultPage };
        Option<int> pageSizeOption = new("--page-size") { DefaultValueFactory = _ => PageRequest.DefaultPageSize };
        Command command = new("list", "List task runs.")
        {
            moduleOption,
            taskOption,
            workerGroupOption,
            statusOption,
            pageOption,
            pageSizeOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string? tenantId = parseResult.GetValue(globalOptions.TenantOption);

            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(TaskRuntimeAdminOperationNames.RunsList, TaskRuntimeAdminPermissions.RunsRead),
                tenantId,
                requireTenant: false,
                async (provider, token) =>
                {
                    if (!TaskRunStatusNames.TryParseOptional(parseResult.GetValue(statusOption), out TaskRunStatus? status))
                    {
                        return Result.Failure<IReadOnlyList<TaskRunSummary>>(TaskRuntimeApplicationErrors.InvalidStatus);
                    }

                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<IReadOnlyList<TaskRunSummary>> result = await dispatcher.QueryAsync(
                        new ListTaskRunsQuery(
                            parseResult.GetValue(moduleOption),
                            parseResult.GetValue(taskOption),
                            parseResult.GetValue(workerGroupOption),
                            status,
                            tenantId,
                            parseResult.GetValue(pageOption),
                            parseResult.GetValue(pageSizeOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        WriteRunSummaries(result.Value, parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table);
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateStatsCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<string?> moduleOption = new("--module") { Description = "Filter by module name." };
        Option<string?> taskOption = new("--task") { Description = "Filter by task name." };
        Option<string?> workerGroupOption = new("--worker-group") { Description = "Filter by worker group." };
        Command command = new("stats", "Show task run counts by status.")
        {
            moduleOption,
            taskOption,
            workerGroupOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string? tenantId = parseResult.GetValue(globalOptions.TenantOption);

            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(TaskRuntimeAdminOperationNames.RunsStats, TaskRuntimeAdminPermissions.RunsRead),
                tenantId,
                requireTenant: false,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<TaskRunStats> result = await dispatcher.QueryAsync(
                        new GetTaskRunStatsQuery(
                            parseResult.GetValue(moduleOption),
                            parseResult.GetValue(taskOption),
                            parseResult.GetValue(workerGroupOption),
                            tenantId),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        WriteRunStats(result.Value, parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table);
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateGetCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> runIdOption = CreateRunIdOption();
        Command command = new("get", "Get task run details.")
        {
            runIdOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string? tenantId = parseResult.GetValue(globalOptions.TenantOption);

            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(TaskRuntimeAdminOperationNames.RunsGet, TaskRuntimeAdminPermissions.RunsRead),
                tenantId,
                requireTenant: false,
                async (provider, token) =>
                {
                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    Result<TaskRunDetails> result = await dispatcher.QueryAsync(
                        new GetTaskRunQuery(parseResult.GetRequiredValue(runIdOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        WriteRunDetails(result.Value, parseResult.GetValue(globalOptions.OutputOption) ?? AdminCliOutput.Table);
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateControlCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> runIdOption = CreateRunIdOption();
        Option<string> commandNameOption = new("--command") { Required = true, Description = "Control command name, e.g. tasks.cancel." };
        Option<string?> payloadJsonOption = new("--payload-json") { Description = "Inline JSON payload. Defaults to {}." };
        Option<FileInfo?> payloadFileOption = new("--payload-file") { Description = "Path to a JSON payload file." };
        Option<DateTimeOffset?> expiresAtOption = new("--expires-at") { Description = "Optional UTC expiration timestamp." };
        Option<bool> yesOption = CreateYesOption();
        Command command = new("control", "Send a control message to a task run.")
        {
            runIdOption,
            commandNameOption,
            payloadJsonOption,
            payloadFileOption,
            expiresAtOption,
            yesOption
        };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string? tenantId = parseResult.GetValue(globalOptions.TenantOption);

            return await executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(TaskRuntimeAdminOperationNames.RunsControl, TaskRuntimeAdminPermissions.RunsControl),
                tenantId,
                requireTenant: false,
                async (provider, token) =>
                {
                    if (!parseResult.GetValue(yesOption))
                    {
                        return Result.Failure<TaskControlMessage>(AdminErrors.ConfirmationRequired);
                    }

                    Result<string> payload = await ReadPayloadAsync(
                            parseResult.GetValue(payloadJsonOption),
                            parseResult.GetValue(payloadFileOption),
                            token,
                            defaultPayloadJson: "{}")
                        .ConfigureAwait(false);

                    if (payload.IsFailure)
                    {
                        return Result.Failure<TaskControlMessage>(payload.Error);
                    }

                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    IAdminActorContext actorContext = provider.GetRequiredService<IAdminActorContext>();
                    Result<TaskControlMessage> result = await dispatcher.SendAsync(
                        new SendTaskControlMessageCommand(
                            parseResult.GetRequiredValue(runIdOption),
                            parseResult.GetRequiredValue(commandNameOption),
                            payload.Value,
                            parseResult.GetValue(expiresAtOption),
                            actorContext.Actor?.Id),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage(
                            $"Control message '{result.Value.CommandName}' enqueued for task run '{result.Value.RunId}'.");
                    }

                    return result;
                },
                cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private static Command CreateEnqueueCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<string> moduleOption = new("--module") { Required = true };
        Option<string> taskOption = new("--task") { Required = true };
        Option<string?> payloadJsonOption = new("--payload-json") { Description = "Inline JSON payload." };
        Option<FileInfo?> payloadFileOption = new("--payload-file") { Description = "Path to a JSON payload file." };
        Option<string> workerGroupOption = new("--worker-group") { DefaultValueFactory = _ => TaskWorkerGroups.Default };
        Option<DateTimeOffset?> scheduledAtOption = new("--scheduled-at") { Description = "Optional UTC schedule timestamp." };
        Option<int> maxAttemptsOption = new("--max-attempts") { DefaultValueFactory = _ => 1 };
        Option<int> payloadVersionOption = new("--payload-version") { DefaultValueFactory = _ => 1 };
        Option<string?> deduplicationKeyOption = new("--dedupe-key")
        {
            Description = "Optional logical deduplication key for active duplicate suppression."
        };
        Option<Guid?> correlationIdOption = new("--correlation-id") { Description = "Optional correlation id." };
        Command command = new("enqueue", "Enqueue a task run.")
        {
            moduleOption,
            taskOption,
            payloadJsonOption,
            payloadFileOption,
            workerGroupOption,
            scheduledAtOption,
            maxAttemptsOption,
            payloadVersionOption,
            deduplicationKeyOption,
            correlationIdOption
        };
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string? tenantId = parseResult.GetValue(globalOptions.TenantOption);

            return await executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(TaskRuntimeAdminOperationNames.RunsEnqueue, TaskRuntimeAdminPermissions.RunsCreate),
                tenantId,
                requireTenant: false,
                async (provider, token) =>
                {
                    Result<string> payload = await ReadPayloadAsync(
                            parseResult.GetValue(payloadJsonOption),
                            parseResult.GetValue(payloadFileOption),
                            token)
                        .ConfigureAwait(false);

                    if (payload.IsFailure)
                    {
                        return Result.Failure<TaskRunDetails>(payload.Error);
                    }

                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    IAdminActorContext actorContext = provider.GetRequiredService<IAdminActorContext>();
                    Result<TaskRunDetails> result = await dispatcher.SendAsync(
                        new EnqueueTaskRunCommand(
                            RunId: null,
                            parseResult.GetRequiredValue(moduleOption),
                            parseResult.GetRequiredValue(taskOption),
                            payload.Value,
                            parseResult.GetValue(scheduledAtOption),
                            parseResult.GetValue(workerGroupOption) ?? TaskWorkerGroups.Default,
                            tenantId,
                            parseResult.GetValue(correlationIdOption),
                            actorContext.Actor?.Id,
                            parseResult.GetValue(maxAttemptsOption),
                            parseResult.GetValue(payloadVersionOption),
                            parseResult.GetValue(deduplicationKeyOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage($"Enqueued task run '{result.Value.Summary.RunId}'.");
                    }

                    return result;
                },
                cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private static Command CreateCancelCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> runIdOption = CreateRunIdOption();
        Option<bool> yesOption = CreateYesOption();
        Command command = new("cancel", "Request cancellation for a task run.")
        {
            runIdOption,
            yesOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string? tenantId = parseResult.GetValue(globalOptions.TenantOption);

            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(TaskRuntimeAdminOperationNames.RunsCancel, TaskRuntimeAdminPermissions.RunsCancel),
                tenantId,
                requireTenant: false,
                async (provider, token) =>
                {
                    if (!parseResult.GetValue(yesOption))
                    {
                        return Result.Failure<Unit>(AdminErrors.ConfirmationRequired);
                    }

                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    IAdminActorContext actorContext = provider.GetRequiredService<IAdminActorContext>();
                    Result<Unit> result = await dispatcher.SendAsync(
                        new CancelTaskRunCommand(parseResult.GetRequiredValue(runIdOption), actorContext.Actor?.Id),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage("Task cancellation requested.");
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static Command CreateRetryCommand(IServiceProvider services, AdminCliGlobalOptions globalOptions)
    {
        Option<Guid> runIdOption = CreateRunIdOption();
        Option<DateTimeOffset?> scheduledAtOption = new("--scheduled-at") { Description = "Optional UTC retry schedule timestamp." };
        Option<bool> yesOption = CreateYesOption();
        Command command = new("retry", "Retry a terminal or retry-scheduled task run.")
        {
            runIdOption,
            scheduledAtOption,
            yesOption
        };
        command.SetAction((parseResult, cancellationToken) =>
        {
            AdminCliExecutor executor = services.GetRequiredService<AdminCliExecutor>();
            string? tenantId = parseResult.GetValue(globalOptions.TenantOption);

            return executor.ExecuteAsync(
                parseResult,
                AdminOperation.Create(TaskRuntimeAdminOperationNames.RunsRetry, TaskRuntimeAdminPermissions.RunsRetry),
                tenantId,
                requireTenant: false,
                async (provider, token) =>
                {
                    if (!parseResult.GetValue(yesOption))
                    {
                        return Result.Failure<Unit>(AdminErrors.ConfirmationRequired);
                    }

                    IRequestDispatcher dispatcher = provider.GetRequiredService<IRequestDispatcher>();
                    IAdminActorContext actorContext = provider.GetRequiredService<IAdminActorContext>();
                    Result<Unit> result = await dispatcher.SendAsync(
                        new RetryTaskRunCommand(
                            parseResult.GetRequiredValue(runIdOption),
                            actorContext.Actor?.Id,
                            parseResult.GetValue(scheduledAtOption)),
                        token).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        AdminCliOutput.WriteMessage("Task retry scheduled.");
                    }

                    return result;
                },
                cancellationToken);
        });

        return command;
    }

    private static void WriteRunSummaries(IReadOnlyCollection<TaskRunSummary> runs, string output) =>
        AdminCliOutput.WriteRows(
            runs,
            output,
            [
                ("RunId", run => run.RunId.ToString()),
                ("Module", run => run.ModuleName),
                ("Task", run => run.TaskName),
                ("WorkerGroup", run => run.WorkerGroup),
                ("Version", run => run.PayloadVersion.ToString(CultureInfo.InvariantCulture)),
                ("Status", run => TaskRunStatusNames.ToWireName(run.Status)),
                ("Attempts", run => $"{run.Attempts.ToString(CultureInfo.InvariantCulture)}/{run.MaxAttempts.ToString(CultureInfo.InvariantCulture)}"),
                ("Tenant", run => run.TenantId ?? string.Empty),
                ("CreatedAtUtc", run => run.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture))
            ]);

    private static void WriteRunDetails(TaskRunDetails run, string output)
    {
        if (string.Equals(AdminCliOutput.NormalizeFormat(output), AdminCliOutput.Json, StringComparison.Ordinal))
        {
            AdminCliOutput.WriteObject(run, output);
            return;
        }

        WriteRunSummaries([run.Summary], output);
        AdminCliOutput.WriteMessage($"Payload: {run.PayloadJson}");
        if (!string.IsNullOrWhiteSpace(run.Summary.LastError))
        {
            AdminCliOutput.WriteMessage($"LastError: {run.Summary.LastError}");
        }
    }

    private static void WriteRunStats(TaskRunStats stats, string output)
    {
        if (string.Equals(AdminCliOutput.NormalizeFormat(output), AdminCliOutput.Json, StringComparison.Ordinal))
        {
            AdminCliOutput.WriteObject(stats, output);
            return;
        }

        AdminCliOutput.WriteRows(
            stats.StatusCounts,
            output,
            [
                ("Status", item => TaskRunStatusNames.ToWireName(item.Status)),
                ("Count", item => item.Count.ToString(CultureInfo.InvariantCulture))
            ]);
        AdminCliOutput.WriteMessage($"Total: {stats.Total.ToString(CultureInfo.InvariantCulture)}");
    }

    private static async Task<Result<string>> ReadPayloadAsync(
        string? payloadJson,
        FileInfo? payloadFile,
        CancellationToken cancellationToken,
        string? defaultPayloadJson = null)
    {
        if (!string.IsNullOrWhiteSpace(payloadJson) && payloadFile is not null)
        {
            return Result.Failure<string>(TaskRuntimeApplicationErrors.PayloadSourceConflict);
        }

        if (!string.IsNullOrWhiteSpace(payloadJson))
        {
            return Result.Success(payloadJson);
        }

        if (payloadFile is null)
        {
            return defaultPayloadJson is null
                ? Result.Failure<string>(TaskRuntimeApplicationErrors.PayloadRequired)
                : Result.Success(defaultPayloadJson);
        }

        if (!payloadFile.Exists)
        {
            return Result.Failure<string>(TaskRuntimeApplicationErrors.PayloadFileNotFound);
        }

        string payload = await File.ReadAllTextAsync(payloadFile.FullName, cancellationToken).ConfigureAwait(false);
        return Result.Success(payload);
    }

    private static Option<Guid> CreateRunIdOption() =>
        new("--run-id")
        {
            Description = "Task run id.",
            Required = true
        };

    private static Option<bool> CreateYesOption() =>
        new("--yes")
        {
            Description = "Confirm this operation."
        };
}
