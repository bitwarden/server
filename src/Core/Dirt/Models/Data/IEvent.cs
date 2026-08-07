// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Enums;

namespace Bit.Core.Models.Data;

public interface IEvent
{
    EventType Type { get; set; }
    Guid? UserId { get; set; }
    Guid? OrganizationId { get; set; }
    Guid? InstallationId { get; set; }
    Guid? ProviderId { get; set; }
    Guid? CipherId { get; set; }
    Guid? CollectionId { get; set; }
    Guid? GroupId { get; set; }
    Guid? PolicyId { get; set; }
    Guid? OrganizationUserId { get; set; }
    Guid? ProviderUserId { get; set; }
    Guid? ProviderOrganizationId { get; set; }
    Guid? ActingUserId { get; set; }
    DeviceType? DeviceType { get; set; }
    string IpAddress { get; set; }
    DateTime Date { get; set; }
    EventSystemUser? SystemUser { get; set; }
    string DomainName { get; set; }
    /// <summary>
    /// A snapshot of the client organization's name at the time a provider-organization event
    /// (created/added/removed/vault accessed) was logged. ProviderOrganization rows are hard-deleted
    /// on unlink, so the name can no longer be resolved by joining through ProviderOrganizationId
    /// once that happens - this column preserves it permanently on the event itself.
    /// </summary>
    string OrganizationName { get; set; }
    Guid? SecretId { get; set; }
    Guid? ProjectId { get; set; }
    Guid? ServiceAccountId { get; set; }
    Guid? GrantedServiceAccountId { get; set; }
    Guid? SendId { get; set; }
}
