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
}
