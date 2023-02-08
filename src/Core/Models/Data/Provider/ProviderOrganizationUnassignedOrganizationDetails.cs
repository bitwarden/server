using Bit.Core.Enums;

namespace Bit.Core.Models.Data;

public class ProviderOrganizationUnassignedOrganizationDetails
{
    public Guid OrganizationId { get; set; }

    public string Name { get; set; }

    public PlanType PlanType { get; set; }

    public string OwnerEmail { get; set; }
}
