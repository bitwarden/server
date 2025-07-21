// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Data.Organizations.OrganizationSponsorships;

namespace Bit.Core.Models.Api.Response.OrganizationSponsorships;

public class OrganizationSponsorshipSyncResponseModel
{
    public IEnumerable<OrganizationSponsorshipResponseModel> SponsorshipsBatch { get; set; }

    public OrganizationSponsorshipSyncResponseModel() { }

    public OrganizationSponsorshipSyncResponseModel(OrganizationSponsorshipSyncData syncData)
    {
        if (syncData == null)
        {
            return;
        }
        SponsorshipsBatch = syncData.SponsorshipsBatch.Select(o => new OrganizationSponsorshipResponseModel(o));

    }

    public OrganizationSponsorshipSyncData ToOrganizationSponsorshipSync()
    {
        return new OrganizationSponsorshipSyncData()
        {
            SponsorshipsBatch = SponsorshipsBatch.Select(o => o.ToOrganizationSponsorship())
        };
    }

}
