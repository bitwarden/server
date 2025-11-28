// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Data.Organizations.OrganizationSponsorships;

public class OrganizationSponsorshipSyncData
{
    public string BillingSyncKey { get; set; }
    public Guid SponsoringOrganizationCloudId { get; set; }
    public IEnumerable<OrganizationSponsorshipData> SponsorshipsBatch { get; set; }
}
