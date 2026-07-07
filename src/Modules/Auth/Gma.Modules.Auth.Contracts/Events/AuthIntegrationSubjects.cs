namespace Gma.Modules.Auth.Contracts;

using Gma.Framework.Messaging;

public static class AuthIntegrationSubjects
{
    public static string MemberRegistered => CreateMemberRegistered();
    public static string MemberDisabled => CreateMemberDisabled();
    public static string MemberEnabled => CreateMemberEnabled();
    public static string MemberSessionsRevoked => CreateMemberSessionsRevoked();

    public static string CreateMemberRegistered(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, AuthModuleMetadata.Name, MemberRegisteredIntegrationEvent.EventType, MemberRegisteredIntegrationEvent.EventVersion);

    public static string CreateMemberDisabled(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, AuthModuleMetadata.Name, MemberDisabledIntegrationEvent.EventType, MemberDisabledIntegrationEvent.EventVersion);

    public static string CreateMemberEnabled(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, AuthModuleMetadata.Name, MemberEnabledIntegrationEvent.EventType, MemberEnabledIntegrationEvent.EventVersion);

    public static string CreateMemberSessionsRevoked(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, AuthModuleMetadata.Name, MemberSessionsRevokedIntegrationEvent.EventType, MemberSessionsRevokedIntegrationEvent.EventVersion);
}
