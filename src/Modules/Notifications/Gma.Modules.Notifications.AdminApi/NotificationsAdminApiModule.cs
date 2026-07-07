namespace Gma.Modules.Notifications.AdminApi;

using System.Net.ServerSentEvents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Gma.Modules.Notifications.Admin.Contracts;
using Gma.Modules.Notifications.Application;
using Gma.Modules.Notifications.Application.Commands;
using Gma.Modules.Notifications.Application.Queries;
using Gma.Modules.Notifications.Contracts;
using Gma.Modules.Notifications.Persistence;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Tenancy;

public sealed class NotificationsAdminApiModule : IAdminApiModule
{
    public string Name => NotificationsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(NotificationsProfiles.Default, "Gma.Modules.Notifications.AdminApi");
        builder.Services.AddNotificationsApplication(builder.Configuration);
        builder.AddNotificationsPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder history = endpoints.MapGroup("/api/admin/notifications")
            .WithModuleName(this.Name)
            .WithTags("Notifications Admin")
            .RequireAuthorization();

        history.MapGet("/", async (
            string? userId,
            bool? unreadOnly,
            int? page,
            int? pageSize,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(NotificationsAdminOperationNames.HistoryList, NotificationsAdminPermissions.HistoryRead),
                requireTenant: true,
                token => dispatcher.QueryAsync(
                    new ListTenantNotificationHistoryQuery(
                        userId,
                        unreadOnly ?? false,
                        page ?? PageRequest.DefaultPage,
                        pageSize ?? PageRequest.DefaultPageSize),
                    token),
                cancellationToken).ConfigureAwait(false));

        history.MapGet("/{notificationId:guid}", async (
            Guid notificationId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(NotificationsAdminOperationNames.HistoryGet, NotificationsAdminPermissions.HistoryRead),
                requireTenant: true,
                token => dispatcher.QueryAsync(new GetTenantNotificationHistoryItemQuery(notificationId), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        history.MapGet("/broadcasts", async (
            int? page,
            int? pageSize,
            HttpContext httpContext,
            AdminApiExecutor executor,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(NotificationsAdminOperationNames.BroadcastsList, NotificationsAdminPermissions.BroadcastsRead),
                requireTenant: true,
                token => dispatcher.QueryAsync(
                    new ListTenantNotificationBroadcastsQuery(
                        RequiredTenantId(tenantContext),
                        page ?? PageRequest.DefaultPage,
                        pageSize ?? PageRequest.DefaultPageSize),
                    token),
                cancellationToken).ConfigureAwait(false));

        history.MapPost("/broadcasts", async (
            AdminCreateNotificationBroadcastRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(NotificationsAdminOperationNames.BroadcastsCreate, NotificationsAdminPermissions.BroadcastsCreate),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new CreateNotificationBroadcastCommand(
                        request.Audience,
                        RequiredTenantId(tenantContext),
                        NotificationsModuleMetadata.Name,
                        request.Name,
                        request.Version,
                        request.Title,
                        request.Body,
                        request.Severity,
                        request.OccurredAtUtc,
                        PayloadJson(request)),
                    token),
                cancellationToken).ConfigureAwait(false));

        history.MapGet("/platform-broadcasts", async (
            int? page,
            int? pageSize,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(NotificationsAdminOperationNames.BroadcastsList, NotificationsAdminPermissions.BroadcastsRead),
                requireTenant: false,
                token => dispatcher.QueryAsync(
                    new ListPlatformNotificationBroadcastsQuery(
                        page ?? PageRequest.DefaultPage,
                        pageSize ?? PageRequest.DefaultPageSize),
                    token),
                cancellationToken).ConfigureAwait(false));

        history.MapPost("/platform-broadcasts", async (
            AdminCreateNotificationBroadcastRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(NotificationsAdminOperationNames.BroadcastsCreate, NotificationsAdminPermissions.BroadcastsCreate),
                requireTenant: false,
                token => dispatcher.SendAsync(
                    new CreateNotificationBroadcastCommand(
                        request.Audience,
                        null,
                        NotificationsModuleMetadata.Name,
                        request.Name,
                        request.Version,
                        request.Title,
                        request.Body,
                        request.Severity,
                        request.OccurredAtUtc,
                        PayloadJson(request)),
                    token),
                cancellationToken).ConfigureAwait(false));

        history.MapGet("/broadcasts/inbox", async (
            bool? unreadOnly,
            int? page,
            int? pageSize,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IAdminActorContext actorContext,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(NotificationsAdminOperationNames.BroadcastsInboxList, NotificationsAdminPermissions.BroadcastsRead),
                requireTenant: true,
                token => dispatcher.QueryAsync(
                    new ListNotificationBroadcastsQuery(
                        RequiredTenantId(tenantContext),
                        NotificationBroadcastRecipientKind.Admin,
                        RequiredActorId(actorContext),
                        unreadOnly ?? false,
                        page ?? PageRequest.DefaultPage,
                        pageSize ?? PageRequest.DefaultPageSize),
                    token),
                cancellationToken).ConfigureAwait(false));

        history.MapPost("/broadcasts/inbox/{broadcastId:guid}/read", async (
            Guid broadcastId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IAdminActorContext actorContext,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(NotificationsAdminOperationNames.BroadcastsInboxMarkRead, NotificationsAdminPermissions.BroadcastsRead),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new MarkNotificationBroadcastReadCommand(
                        broadcastId,
                        RequiredTenantId(tenantContext),
                        NotificationBroadcastRecipientKind.Admin,
                        RequiredActorId(actorContext)),
                    token),
                cancellationToken,
                onSuccess: _ => Results.NoContent(),
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        history.MapPost("/broadcasts/inbox/read-all", async (
            HttpContext httpContext,
            AdminApiExecutor executor,
            IAdminActorContext actorContext,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(NotificationsAdminOperationNames.BroadcastsInboxMarkRead, NotificationsAdminPermissions.BroadcastsRead),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new MarkAllNotificationBroadcastsReadCommand(
                        RequiredTenantId(tenantContext),
                        NotificationBroadcastRecipientKind.Admin,
                        RequiredActorId(actorContext)),
                    token),
                cancellationToken).ConfigureAwait(false));

        history.MapGet("/broadcasts/inbox/stream", async (
            long? afterSequence,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IAdminActorContext actorContext,
            ITenantContext tenantContext,
            IRequestDispatcher dispatcher,
            ILogger<NotificationsAdminApiModule> logger,
            IOptions<NotificationStreamOptions> streamOptions,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(NotificationsAdminOperationNames.BroadcastsInboxStream, NotificationsAdminPermissions.BroadcastsRead),
                requireTenant: true,
                async token =>
                {
                    if (afterSequence is < 0)
                    {
                        return Result.Failure<IResult>(NotificationsApplicationErrors.StreamCursorInvalid);
                    }

                    string tenantId = RequiredTenantId(tenantContext);
                    string actorId = RequiredActorId(actorContext);
                    long cursor;
                    if (afterSequence.HasValue)
                    {
                        cursor = afterSequence.Value;
                    }
                    else
                    {
                        Result<long> cursorResult = await ResolveCurrentBroadcastCursorAsync(
                            dispatcher,
                            tenantId,
                            NotificationBroadcastRecipientKind.Admin,
                            actorId,
                            token).ConfigureAwait(false);
                        if (cursorResult.IsFailure)
                        {
                            return Result.Failure<IResult>(cursorResult.Error);
                        }

                        cursor = cursorResult.Value;
                    }

                    return Result.Success<IResult>(TypedResults.ServerSentEvents(
                        StreamBroadcastsAsync(
                            dispatcher,
                            tenantId,
                            NotificationBroadcastRecipientKind.Admin,
                            actorId,
                            cursor,
                            streamOptions.Value,
                            logger,
                            httpContext.RequestAborted)));
                },
                cancellationToken,
                onSuccess: result => result).ConfigureAwait(false));

        history.MapGet("/history/stream", async (
            long? afterSequence,
            string? userId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            ILogger<NotificationsAdminApiModule> logger,
            IOptions<NotificationStreamOptions> streamOptions,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(NotificationsAdminOperationNames.HistoryStream, NotificationsAdminPermissions.HistoryRead),
                requireTenant: true,
                async token =>
                {
                    if (afterSequence is < 0)
                    {
                        return Result.Failure<IResult>(NotificationsApplicationErrors.StreamCursorInvalid);
                    }

                    long cursor;
                    if (afterSequence.HasValue)
                    {
                        cursor = afterSequence.Value;
                    }
                    else
                    {
                        Result<long> cursorResult = await ResolveCurrentTenantCursorAsync(
                            dispatcher,
                            userId,
                            token).ConfigureAwait(false);
                        if (cursorResult.IsFailure)
                        {
                            return Result.Failure<IResult>(cursorResult.Error);
                        }

                        cursor = cursorResult.Value;
                    }

                    return Result.Success<IResult>(TypedResults.ServerSentEvents(
                        StreamTenantHistoryAsync(
                            dispatcher,
                            userId,
                            cursor,
                            streamOptions.Value,
                            logger,
                            httpContext.RequestAborted)));
                },
                cancellationToken,
                onSuccess: result => result).ConfigureAwait(false));
    }

    private static Task<Result<long>> ResolveCurrentTenantCursorAsync(
        IRequestDispatcher dispatcher,
        string? userId,
        CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(
            new GetTenantNotificationStreamCursorQuery(userId),
            cancellationToken);

    private static Task<Result<long>> ResolveCurrentBroadcastCursorAsync(
        IRequestDispatcher dispatcher,
        string? tenantId,
        NotificationBroadcastRecipientKind recipientKind,
        string recipientId,
        CancellationToken cancellationToken) =>
        dispatcher.QueryAsync(
            new GetNotificationBroadcastStreamCursorQuery(tenantId, recipientKind, recipientId),
            cancellationToken);

    private static async IAsyncEnumerable<SseItem<AdminNotificationHistoryItem>> StreamTenantHistoryAsync(
        IRequestDispatcher dispatcher,
        string? userId,
        long initialCursor,
        NotificationStreamOptions options,
        ILogger logger,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long afterSequence = initialCursor;
        using PeriodicTimer pollTimer = new(options.PollInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            Result<IReadOnlyList<AdminNotificationHistoryItem>> result = await dispatcher.QueryAsync(
                new StreamTenantNotificationHistoryQuery(userId, afterSequence, options.BatchSize),
                cancellationToken).ConfigureAwait(false);

            if (result.IsFailure)
            {
                LogStreamQueryFailure(logger, "admin-history", result.Error);
                yield break;
            }

            foreach (AdminNotificationHistoryItem item in result.Value)
            {
                afterSequence = item.StreamSequence;
                yield return new SseItem<AdminNotificationHistoryItem>(item, "notification");
            }

            if (!await pollTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                yield break;
            }
        }
    }

    private static async IAsyncEnumerable<SseItem<NotificationBroadcastItem>> StreamBroadcastsAsync(
        IRequestDispatcher dispatcher,
        string? tenantId,
        NotificationBroadcastRecipientKind recipientKind,
        string recipientId,
        long initialCursor,
        NotificationStreamOptions options,
        ILogger logger,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long afterSequence = initialCursor;
        using PeriodicTimer pollTimer = new(options.PollInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            Result<IReadOnlyList<NotificationBroadcastItem>> result = await dispatcher.QueryAsync(
                new StreamNotificationBroadcastsQuery(
                    tenantId,
                    recipientKind,
                    recipientId,
                    afterSequence,
                    options.BatchSize),
                cancellationToken).ConfigureAwait(false);

            if (result.IsFailure)
            {
                LogStreamQueryFailure(logger, "admin-broadcast", result.Error);
                yield break;
            }

            foreach (NotificationBroadcastItem item in result.Value)
            {
                afterSequence = item.StreamSequence;
                yield return new SseItem<NotificationBroadcastItem>(item, "notification-broadcast");
            }

            if (!await pollTimer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                yield break;
            }
        }
    }

    private static string RequiredTenantId(ITenantContext tenantContext) =>
        tenantContext.TenantId ?? string.Empty;

    private static string RequiredActorId(IAdminActorContext actorContext) =>
        actorContext.Actor?.Id ?? string.Empty;

    private static string PayloadJson(AdminCreateNotificationBroadcastRequest request) =>
        request.Payload?.GetRawText() ?? "{}";

    private static void LogStreamQueryFailure(ILogger logger, string streamName, Error error)
    {
        logger.LogWarning(
            "Notification {StreamName} stream query failed and the stream will be closed. Error: {ErrorCode}.",
            streamName,
            error.Code);
    }

    private static readonly ApiErrorStatusCodeMap AdminErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new ApiErrorStatusCode(NotificationsApplicationErrors.NotificationNotFound.Code, StatusCodes.Status404NotFound),
        new ApiErrorStatusCode(NotificationsApplicationErrors.BroadcastNotFound.Code, StatusCodes.Status404NotFound));
}
