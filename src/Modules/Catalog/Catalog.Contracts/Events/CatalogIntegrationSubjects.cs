namespace Catalog.Contracts;

using Gma.Framework.Messaging;

public static class CatalogIntegrationSubjects
{
    public static string ItemCreated => CreateItemCreated();
    public static string ItemUpdated => CreateItemUpdated();
    public static string ItemDiscontinued => CreateItemDiscontinued();

    public static string CreateItemCreated(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, CatalogModuleMetadata.Name, CatalogItemCreatedIntegrationEvent.EventType, CatalogItemCreatedIntegrationEvent.EventVersion);

    public static string CreateItemUpdated(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, CatalogModuleMetadata.Name, CatalogItemUpdatedIntegrationEvent.EventType, CatalogItemUpdatedIntegrationEvent.EventVersion);

    public static string CreateItemDiscontinued(string subjectPrefix = IntegrationEventNaming.DefaultSubjectPrefix) =>
        IntegrationEventNaming.CreateSubject(subjectPrefix, CatalogModuleMetadata.Name, CatalogItemDiscontinuedIntegrationEvent.EventType, CatalogItemDiscontinuedIntegrationEvent.EventVersion);
}
