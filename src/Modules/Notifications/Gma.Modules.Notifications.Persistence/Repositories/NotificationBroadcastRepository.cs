namespace Gma.Modules.Notifications.Persistence.Repositories;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Gma.Modules.Notifications.Application;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Gma.Modules.Notifications.Domain.Aggregates;
using Gma.Modules.Notifications.Domain.Entities;
using Gma.Modules.Notifications.Domain.ValueObjects;
using Gma.Framework.Naming;
using Gma.Framework.Pagination;
using Gma.Framework.Runtime.Identity;
using DomainAudience = Gma.Modules.Notifications.Domain.ValueObjects.NotificationBroadcastAudience;
using DomainRecipientKind = Gma.Modules.Notifications.Domain.ValueObjects.NotificationBroadcastRecipientKind;

internal sealed class NotificationBroadcastRepository(NotificationsDbContext dbContext, IIdGenerator idGenerator)
    : INotificationBroadcastRepository
{
    private const int MarkAllReadBatchSize = 500;
    private const DomainAudience TenantUsersAudience = DomainAudience.TenantUsers;
    private const DomainAudience TenantAdminsAudience = DomainAudience.TenantAdmins;
    private const DomainAudience PlatformUsersAudience = DomainAudience.PlatformUsers;
    private const DomainAudience PlatformAdminsAudience = DomainAudience.PlatformAdmins;
    private const DomainRecipientKind UserRecipientKind = DomainRecipientKind.User;
    private const DomainRecipientKind AdminRecipientKind = DomainRecipientKind.Admin;

    public async Task AddAsync(NotificationBroadcast broadcast, CancellationToken cancellationToken)
    {
        await dbContext.NotificationBroadcasts.AddAsync(broadcast, cancellationToken).ConfigureAwait(false);
    }

    public async Task<NotificationBroadcastItem?> GetVisibleAsync(
        Guid broadcastId,
        NotificationBroadcastRecipientContext recipient,
        CancellationToken cancellationToken)
    {
        IQueryable<NotificationBroadcast> visibleBroadcasts = ApplyVisibility(
            dbContext.NotificationBroadcasts.AsNoTracking(),
            recipient);

        NotificationBroadcast? broadcast = await visibleBroadcasts
            .FirstOrDefaultAsync(item => item.Id == broadcastId, cancellationToken)
            .ConfigureAwait(false);

        if (broadcast is null)
        {
            return null;
        }

        NotificationBroadcastRead? read = await dbContext.NotificationBroadcastReads
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.BroadcastId == broadcastId &&
                    item.RecipientScope == recipient.RecipientScope &&
                    item.RecipientKind == recipient.RecipientKind &&
                    item.Recipient == recipient.Recipient,
                cancellationToken)
            .ConfigureAwait(false);

        return Map(broadcast, read?.ReadAtUtc);
    }

    public async Task<NotificationBroadcastListResponse> ListVisibleAsync(
        NotificationBroadcastRecipientContext recipient,
        bool unreadOnly,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<NotificationBroadcast> visibleBroadcasts = ApplyVisibility(
            dbContext.NotificationBroadcasts.AsNoTracking(),
            recipient);

        IQueryable<NotificationBroadcastRead> recipientReads = this.RecipientReads(recipient);

        int unreadCount = await visibleBroadcasts
            .CountAsync(
                broadcast => !recipientReads.Any(read => read.BroadcastId == broadcast.Id),
                cancellationToken)
            .ConfigureAwait(false);

        IQueryable<NotificationBroadcast> filtered = unreadOnly
            ? visibleBroadcasts.Where(
                broadcast => !recipientReads.Any(read => read.BroadcastId == broadcast.Id))
            : visibleBroadcasts;

        int totalCount = await filtered.CountAsync(cancellationToken).ConfigureAwait(false);
        NotificationBroadcast[] broadcasts = await filtered
            .OrderByDescending(broadcast => broadcast.OccurredAtUtc)
            .ThenByDescending(broadcast => broadcast.CreatedAtUtc)
            .ThenBy(broadcast => broadcast.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid[] broadcastIds = broadcasts.Select(broadcast => broadcast.Id).ToArray();
        NotificationBroadcastRead[] reads = broadcastIds.Length == 0
            ? []
            : await recipientReads
                .Where(read => broadcastIds.Contains(read.BroadcastId))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

        Dictionary<Guid, DateTimeOffset> readByBroadcastId = reads.ToDictionary(
            read => read.BroadcastId,
            read => read.ReadAtUtc);

        return new NotificationBroadcastListResponse(
            broadcasts
                .Select(broadcast => Map(
                    broadcast,
                    readByBroadcastId.TryGetValue(broadcast.Id, out DateTimeOffset readAtUtc)
                        ? readAtUtc
                        : null))
                .ToArray(),
            pageRequest.Page,
            pageRequest.PageSize,
            totalCount,
            unreadCount);
    }

    public async Task<long> GetCurrentStreamSequenceAsync(
        NotificationBroadcastRecipientContext recipient,
        CancellationToken cancellationToken)
    {
        long? cursor = await ApplyVisibility(
                dbContext.NotificationBroadcasts.AsNoTracking(),
                recipient)
            .MaxAsync(broadcast => (long?)broadcast.StreamSequence, cancellationToken)
            .ConfigureAwait(false);

        return cursor ?? 0;
    }

    public async Task<IReadOnlyList<NotificationBroadcastItem>> ListNewVisibleAsync(
        NotificationBroadcastRecipientContext recipient,
        long afterStreamSequence,
        int batchSize,
        CancellationToken cancellationToken)
    {
        IQueryable<NotificationBroadcastRead> recipientReads = this.RecipientReads(recipient);

        NotificationBroadcast[] broadcasts = await ApplyVisibility(
                dbContext.NotificationBroadcasts.AsNoTracking(),
                recipient)
            .Where(broadcast => broadcast.StreamSequence > afterStreamSequence)
            .OrderBy(broadcast => broadcast.StreamSequence)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        Guid[] broadcastIds = broadcasts.Select(broadcast => broadcast.Id).ToArray();
        NotificationBroadcastRead[] reads = broadcastIds.Length == 0
            ? []
            : await recipientReads
                .Where(read => broadcastIds.Contains(read.BroadcastId))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

        Dictionary<Guid, DateTimeOffset> readByBroadcastId = reads.ToDictionary(
            read => read.BroadcastId,
            read => read.ReadAtUtc);

        return broadcasts
            .Select(broadcast => Map(
                broadcast,
                readByBroadcastId.TryGetValue(broadcast.Id, out DateTimeOffset readAtUtc)
                    ? readAtUtc
                    : null))
            .ToArray();
    }

    public async Task<bool> MarkReadAsync(
        Guid broadcastId,
        NotificationBroadcastRecipientContext recipient,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken)
    {
        bool visible = await ApplyVisibility(
                dbContext.NotificationBroadcasts.AsNoTracking(),
                recipient)
            .AnyAsync(broadcast => broadcast.Id == broadcastId, cancellationToken)
            .ConfigureAwait(false);

        if (!visible)
        {
            return false;
        }

        await this.InsertReadReceiptIfMissingAsync(
                broadcastId,
                recipient,
                readAtUtc,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    public async Task<int> MarkAllVisibleReadAsync(
        NotificationBroadcastRecipientContext recipient,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken)
    {
        int updatedCount = 0;
        long lastStreamSequence = long.MinValue;

        while (true)
        {
            IQueryable<NotificationBroadcastRead> recipientReads = this.RecipientReads(recipient);
            NotificationBroadcast[] unreadBroadcasts = await ApplyVisibility(
                    dbContext.NotificationBroadcasts.AsNoTracking(),
                    recipient)
                .Where(broadcast =>
                    broadcast.StreamSequence > lastStreamSequence &&
                    !recipientReads.Any(read => read.BroadcastId == broadcast.Id))
                .OrderBy(broadcast => broadcast.StreamSequence)
                .Take(MarkAllReadBatchSize)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            if (unreadBroadcasts.Length == 0)
            {
                break;
            }

            foreach (NotificationBroadcast broadcast in unreadBroadcasts)
            {
                updatedCount += await this.InsertReadReceiptIfMissingAsync(
                        broadcast.Id,
                        recipient,
                        readAtUtc,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            lastStreamSequence = unreadBroadcasts[^1].StreamSequence;
        }

        return updatedCount;
    }

    public async Task<AdminNotificationBroadcastListResponse> ListTenantBroadcastsAsync(
        string tenantId,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        string normalizedTenantId = TenantIds.Normalize(tenantId);
        IQueryable<NotificationBroadcast> broadcasts = dbContext.NotificationBroadcasts
            .AsNoTracking()
            .Where(broadcast =>
                broadcast.TenantId == normalizedTenantId &&
                (broadcast.Audience == TenantUsersAudience ||
                 broadcast.Audience == TenantAdminsAudience));

        return await ListAdminAsync(broadcasts, pageRequest, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdminNotificationBroadcastListResponse> ListPlatformBroadcastsAsync(
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<NotificationBroadcast> broadcasts = dbContext.NotificationBroadcasts
            .AsNoTracking()
            .Where(broadcast =>
                broadcast.TenantId == null &&
                (broadcast.Audience == PlatformUsersAudience ||
                 broadcast.Audience == PlatformAdminsAudience));

        return await ListAdminAsync(broadcasts, pageRequest, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<AdminNotificationBroadcastListResponse> ListAdminAsync(
        IQueryable<NotificationBroadcast> broadcasts,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        int totalCount = await broadcasts.CountAsync(cancellationToken).ConfigureAwait(false);
        NotificationBroadcast[] items = await broadcasts
            .OrderByDescending(broadcast => broadcast.CreatedAtUtc)
            .ThenBy(broadcast => broadcast.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AdminNotificationBroadcastListResponse(
            items.Select(MapAdmin).ToArray(),
            pageRequest.Page,
            pageRequest.PageSize,
            totalCount);
    }

    private IQueryable<NotificationBroadcastRead> RecipientReads(NotificationBroadcastRecipientContext recipient)
    {
        return dbContext.NotificationBroadcastReads
            .AsNoTracking()
            .Where(read =>
                read.RecipientScope == recipient.RecipientScope &&
                read.RecipientKind == recipient.RecipientKind &&
                read.Recipient == recipient.Recipient);
    }

    private async Task<int> InsertReadReceiptIfMissingAsync(
        Guid broadcastId,
        NotificationBroadcastRecipientContext recipient,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken)
    {
        Guid readId = idGenerator.NewId();
        string recipientScope = recipient.RecipientScope;
        string recipientKindValue = recipient.RecipientKindName;
        string recipientId = recipient.RecipientId;

        if (dbContext.Database.IsSqlServer())
        {
            return await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [notifications].[notification_broadcast_reads]
                    ([Id], [BroadcastId], [RecipientScope], [RecipientKind], [RecipientId], [ReadAtUtc])
                SELECT {readId}, {broadcastId}, {recipientScope}, {recipientKindValue}, {recipientId}, {readAtUtc}
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [notifications].[notification_broadcast_reads] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [BroadcastId] = {broadcastId}
                      AND [RecipientScope] = {recipientScope}
                      AND [RecipientKind] = {recipientKindValue}
                      AND [RecipientId] = {recipientId})
                """, cancellationToken).ConfigureAwait(false);
        }

        if (dbContext.Database.IsNpgsql())
        {
            return await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "notifications"."notification_broadcast_reads"
                    ("Id", "BroadcastId", "RecipientScope", "RecipientKind", "RecipientId", "ReadAtUtc")
                VALUES ({readId}, {broadcastId}, {recipientScope}, {recipientKindValue}, {recipientId}, {readAtUtc})
                ON CONFLICT ("BroadcastId", "RecipientScope", "RecipientKind", "RecipientId") DO NOTHING
                """, cancellationToken).ConfigureAwait(false);
        }

        bool alreadyRead = await dbContext.NotificationBroadcastReads
            .AnyAsync(
                read =>
                    read.BroadcastId == broadcastId &&
                    read.RecipientScope == recipientScope &&
                    read.RecipientKind == recipient.RecipientKind &&
                    read.Recipient == recipient.Recipient,
                cancellationToken)
            .ConfigureAwait(false);

        if (alreadyRead)
        {
            return 0;
        }

        NotificationBroadcastRead readReceipt = NotificationBroadcastRead.Create(
            readId,
            broadcastId,
            recipient.TenantId,
            recipient.RecipientKind,
            recipientId,
            readAtUtc).Value;
        await dbContext.NotificationBroadcastReads.AddAsync(readReceipt, cancellationToken).ConfigureAwait(false);
        return 1;
    }

    private static IQueryable<NotificationBroadcast> ApplyVisibility(
        IQueryable<NotificationBroadcast> broadcasts,
        NotificationBroadcastRecipientContext recipient)
    {
        return recipient.RecipientKind switch
        {
            _ when recipient.RecipientKind == UserRecipientKind => broadcasts.Where(broadcast =>
                broadcast.Audience == PlatformUsersAudience ||
                (broadcast.Audience == TenantUsersAudience &&
                 broadcast.TenantId == recipient.TenantId)),
            _ when recipient.RecipientKind == AdminRecipientKind => broadcasts.Where(broadcast =>
                broadcast.Audience == PlatformAdminsAudience ||
                (broadcast.Audience == TenantAdminsAudience &&
                 broadcast.TenantId == recipient.TenantId)),
            _ => broadcasts.Where(_ => false)
        };
    }

    private static NotificationBroadcastItem Map(NotificationBroadcast broadcast, DateTimeOffset? readAtUtc)
    {
        using JsonDocument document = JsonDocument.Parse(broadcast.Payload.Json);
        return new NotificationBroadcastItem(
            broadcast.Id,
            broadcast.TenantId,
            ToContractAudience(broadcast.Audience),
            broadcast.Source.Module,
            broadcast.Source.Name,
            broadcast.Source.Version,
            broadcast.Content.Title,
            broadcast.Content.Body,
            ToContractSeverity(broadcast.Severity),
            broadcast.StreamSequence,
            broadcast.OccurredAtUtc,
            broadcast.CreatedAtUtc,
            readAtUtc,
            document.RootElement.Clone());
    }

    private static AdminNotificationBroadcastItem MapAdmin(NotificationBroadcast broadcast)
    {
        using JsonDocument document = JsonDocument.Parse(broadcast.Payload.Json);
        return new AdminNotificationBroadcastItem(
            broadcast.Id,
            broadcast.TenantId,
            ToContractAudience(broadcast.Audience),
            broadcast.Source.Module,
            broadcast.Source.Name,
            broadcast.Source.Version,
            broadcast.Content.Title,
            broadcast.Content.Body,
            ToContractSeverity(broadcast.Severity),
            broadcast.StreamSequence,
            broadcast.OccurredAtUtc,
            broadcast.CreatedAtUtc,
            document.RootElement.Clone());
    }

    private static Gma.Modules.Notifications.Contracts.NotificationBroadcastAudience ToContractAudience(DomainAudience audience) =>
        audience switch
        {
            DomainAudience.TenantUsers => Contracts.NotificationBroadcastAudience.TenantUsers,
            DomainAudience.TenantAdmins => Contracts.NotificationBroadcastAudience.TenantAdmins,
            DomainAudience.PlatformUsers => Contracts.NotificationBroadcastAudience.PlatformUsers,
            DomainAudience.PlatformAdmins => Contracts.NotificationBroadcastAudience.PlatformAdmins,
            _ => Contracts.NotificationBroadcastAudience.Unknown
        };

    private static Gma.Modules.Notifications.Contracts.NotificationSeverity ToContractSeverity(
        Gma.Modules.Notifications.Domain.ValueObjects.NotificationSeverity severity) =>
        severity switch
        {
            Domain.ValueObjects.NotificationSeverity.Info => Contracts.NotificationSeverity.Info,
            Domain.ValueObjects.NotificationSeverity.Success => Contracts.NotificationSeverity.Success,
            Domain.ValueObjects.NotificationSeverity.Warning => Contracts.NotificationSeverity.Warning,
            Domain.ValueObjects.NotificationSeverity.Error => Contracts.NotificationSeverity.Error,
            _ => Contracts.NotificationSeverity.Unknown
        };
}
