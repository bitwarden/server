using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationSponsorships;

namespace Bit.Core.Models.Api.Response.OrganizationSponsorships;

public class OrganizationSponsorshipResponseModel
{
    public Guid SponsoringOrganizationUserId { get; set; }
    public string FriendlyName { get; set; }
    public string OfferedToEmail { get; set; }
    public PlanSponsorshipType PlanSponsorshipType { get; set; }
    public DateTime? LastSyncDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool ToDelete { get; set; }

    public bool CloudSponsorshipRemoved { get; set; }

    public OrganizationSponsorshipResponseModel() { }

    public OrganizationSponsorshipResponseModel(OrganizationSponsorshipData sponsorshipData)
    {
        SponsoringOrganizationUserId = sponsorshipData.SponsoringOrganizationUserId;
        FriendlyName = sponsorshipData.FriendlyName;
        OfferedToEmail = sponsorshipData.OfferedToEmail;
        PlanSponsorshipType = sponsorshipData.PlanSponsorshipType;
        LastSyncDate = sponsorshipData.LastSyncDate;
        ValidUntil = sponsorshipData.ValidUntil;
        ToDelete = sponsorshipData.ToDelete;
        CloudSponsorshipRemoved = sponsorshipData.CloudSponsorshipRemoved;
    }

    public OrganizationSponsorshipData ToOrganizationSponsorship()
    {
        return new OrganizationSponsorshipData
        {
            SponsoringOrganizationUserId = SponsoringOrganizationUserId,
            FriendlyName = FriendlyName,
            OfferedToEmail = OfferedToEmail,
            PlanSponsorshipType = PlanSponsorshipType,
            LastSyncDate = LastSyncDate,
            ValidUntil = ValidUntil,
            ToDelete = ToDelete,
            CloudSponsorshipRemoved = CloudSponsorshipRemoved,
        };
    }
}
