namespace Gma.Modules.Notifications.Domain.ValueObjects;

using System.Text.Json;
using Gma.Modules.Notifications.Domain.Errors;
using Gma.Framework.Results;

public readonly record struct NotificationPayload
{
    public const int MaxLength = 32768;

    private NotificationPayload(string json) => this.Json = json;

    public string Json { get; }

    public static Result<NotificationPayload> Create(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return Result.Failure<NotificationPayload>(NotificationsDomainErrors.PayloadInvalid);
        }

        if (payloadJson.Length > MaxLength)
        {
            return Result.Failure<NotificationPayload>(NotificationsDomainErrors.PayloadInvalid);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(payloadJson);
            string normalizedJson = JsonSerializer.Serialize(document.RootElement);
            return normalizedJson.Length > MaxLength
                ? Result.Failure<NotificationPayload>(NotificationsDomainErrors.PayloadInvalid)
                : Result.Success(new NotificationPayload(normalizedJson));
        }
        catch (JsonException)
        {
            return Result.Failure<NotificationPayload>(NotificationsDomainErrors.PayloadInvalid);
        }
    }
}
