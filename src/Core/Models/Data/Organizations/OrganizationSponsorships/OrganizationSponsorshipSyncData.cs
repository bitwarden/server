namespace Bit.Core.Models.Data.Organizations.OrganizationSponsorships;

public class OrganizationSponsorshipSyncData
{
    public string BillingSyncKey { get; set; }
    public Guid SponsoringOrganizationCloudId { get; set; }
    public IEnumerable<OrganizationSponsorshipData> SponsorshipsBatch { get; set; }
}
