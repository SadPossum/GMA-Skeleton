namespace Gma.Modules.TaskRuntime.AdminApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
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

public sealed class TaskRuntimeAdminApiModule : IAdminApiModule
{
    public string Name => TaskRuntimeModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(TaskRuntimeProfiles.Default, "Gma.Modules.TaskRuntime.AdminApi");
        builder.Services.AddTaskRuntimeApplication();
        builder.AddTaskRuntimePersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder runs = endpoints.MapGroup("/api/admin/tasks/runs")
            .WithModuleName(this.Name)
            .WithTags("Task Runtime Admin")
            .RequireAuthorization();

        runs.MapGet("/", async (
            string? module,
            string? task,
            string? workerGroup,
            string? status,
            string? tenant,
            int? page,
            int? pageSize,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(TaskRuntimeAdminOperationNames.RunsList, TaskRuntimeAdminPermissions.RunsRead),
                requireTenant: false,
                token =>
                {
                    return !TaskRunStatusNames.TryParseOptional(status, out TaskRunStatus? parsedStatus)
                        ? Task.FromResult(Result.Failure<IReadOnlyList<TaskRunSummary>>(TaskRuntimeApplicationErrors.InvalidStatus))
                        : dispatcher.QueryAsync(
                            new ListTaskRunsQuery(
                                module,
                                task,
                                workerGroup,
                                parsedStatus,
                                tenant,
                                page ?? PageRequest.DefaultPage,
                                pageSize ?? PageRequest.DefaultPageSize),
                            token);
                },
                cancellationToken,
                tenantId: tenant,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        runs.MapGet("/stats", async (
            string? module,
            string? task,
            string? workerGroup,
            string? tenant,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(TaskRuntimeAdminOperationNames.RunsStats, TaskRuntimeAdminPermissions.RunsRead),
                requireTenant: false,
                token => dispatcher.QueryAsync(
                    new GetTaskRunStatsQuery(module, task, workerGroup, tenant),
                    token),
                cancellationToken,
                tenantId: tenant,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        runs.MapGet("/{runId:guid}", async (
            Guid runId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(TaskRuntimeAdminOperationNames.RunsGet, TaskRuntimeAdminPermissions.RunsRead),
                requireTenant: false,
                token => dispatcher.QueryAsync(new GetTaskRunQuery(runId), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        runs.MapPost("/", async (
            EnqueueTaskRunRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(TaskRuntimeAdminOperationNames.RunsEnqueue, TaskRuntimeAdminPermissions.RunsCreate),
                requireTenant: false,
                token => dispatcher.SendAsync(
                    new EnqueueTaskRunCommand(
                        request.RunId,
                        request.Module,
                        request.Task,
                        request.PayloadJson,
                        request.ScheduledAtUtc,
                        request.WorkerGroup ?? TaskWorkerGroups.Default,
                        request.TenantId,
                        request.CorrelationId,
                        ResolveActorId(httpContext),
                        request.MaxAttempts ?? 1,
                        request.PayloadVersion ?? 1,
                        request.DeduplicationKey),
                    token),
                cancellationToken,
                tenantId: request.TenantId,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        runs.MapPost("/{runId:guid}/control", async (
            Guid runId,
            ControlTaskRunRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(TaskRuntimeAdminOperationNames.RunsControl, TaskRuntimeAdminPermissions.RunsControl),
                requireTenant: false,
                token => request.Confirmed
                    ? dispatcher.SendAsync(
                        new SendTaskControlMessageCommand(
                            runId,
                            request.Command,
                            request.PayloadJson ?? "{}",
                            request.ExpiresAtUtc,
                            ResolveActorId(httpContext)),
                        token)
                    : Task.FromResult(Result.Failure<TaskControlMessage>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        runs.MapPost("/{runId:guid}/cancel", async (
            Guid runId,
            ConfirmedRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(TaskRuntimeAdminOperationNames.RunsCancel, TaskRuntimeAdminPermissions.RunsCancel),
                requireTenant: false,
                token => request.Confirmed
                    ? dispatcher.SendAsync(new CancelTaskRunCommand(runId, ResolveActorId(httpContext)), token)
                    : Task.FromResult(Result.Failure<Unit>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        runs.MapPost("/{runId:guid}/retry", async (
            Guid runId,
            RetryTaskRunRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(TaskRuntimeAdminOperationNames.RunsRetry, TaskRuntimeAdminPermissions.RunsRetry),
                requireTenant: false,
                token => request.Confirmed
                    ? dispatcher.SendAsync(new RetryTaskRunCommand(runId, ResolveActorId(httpContext), request.ScheduledAtUtc), token)
                    : Task.FromResult(Result.Failure<Unit>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));
    }

    public sealed record EnqueueTaskRunRequest(
        Guid? RunId,
        string Module,
        string Task,
        string PayloadJson,
        DateTimeOffset? ScheduledAtUtc,
        string? WorkerGroup,
        string? TenantId,
        Guid? CorrelationId,
        int? MaxAttempts,
        int? PayloadVersion,
        string? DeduplicationKey);

    public sealed record ConfirmedRequest(bool Confirmed);

    public sealed record RetryTaskRunRequest(bool Confirmed, DateTimeOffset? ScheduledAtUtc);

    public sealed record ControlTaskRunRequest(
        string Command,
        string? PayloadJson,
        DateTimeOffset? ExpiresAtUtc,
        bool Confirmed);

    private static string? ResolveActorId(HttpContext httpContext) =>
        httpContext.RequestServices.GetService<IAdminActorContext>()?.Actor?.Id;

    private static readonly ApiErrorStatusCodeMap AdminErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(TaskRuntimeApplicationErrors.RunNotFound.Code, StatusCodes.Status404NotFound),
        new(TaskRuntimeApplicationErrors.InvalidPayloadJson.Code, StatusCodes.Status400BadRequest),
        new(TaskRuntimeApplicationErrors.InvalidRunId.Code, StatusCodes.Status400BadRequest),
        new(TaskRuntimeApplicationErrors.InvalidStatus.Code, StatusCodes.Status400BadRequest),
        new(TaskRuntimeApplicationErrors.PayloadRequired.Code, StatusCodes.Status400BadRequest),
        new(TaskRuntimeApplicationErrors.PayloadSourceConflict.Code, StatusCodes.Status400BadRequest),
        new(TaskRuntimeApplicationErrors.PayloadFileNotFound.Code, StatusCodes.Status400BadRequest),
        new(TaskRuntimeApplicationErrors.RunCannotBeCanceled.Code, StatusCodes.Status409Conflict),
        new(TaskRuntimeApplicationErrors.RunCannotBeRetried.Code, StatusCodes.Status409Conflict),
        new(TaskRuntimeApplicationErrors.RunCannotBeControlled.Code, StatusCodes.Status409Conflict),
        new(TaskRuntimeApplicationErrors.InvalidControlMessage.Code, StatusCodes.Status400BadRequest));
}
