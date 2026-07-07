namespace Integration.Tests.Support;

using Gma.Framework.Tasks;

internal sealed record TaskRunSnapshot(
    Guid Id,
    TaskRunStatus Status,
    string? LockedBy,
    string? NodeId,
    int Attempts,
    DateTimeOffset? NextAttemptAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? LastError,
    int? ProgressPercent,
    string? ProgressMessage,
    string? RequestedBy,
    string? CancellationRequestedBy,
    DateTimeOffset? CancellationRequestedAtUtc,
    int PayloadVersion,
    string? DeduplicationKey);
