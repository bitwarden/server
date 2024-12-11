using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationSponsorships;

namespace Bit.Core.Models.Api.Request.OrganizationSponsorships;

public class OrganizationSponsorshipRequestModel
{
    public Guid SponsoringOrganizationUserId { get; set; }
    public string FriendlyName { get; set; }
    public string OfferedToEmail { get; set; }
    public PlanSponsorshipType PlanSponsorshipType { get; set; }
    public DateTime? LastSyncDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool ToDelete { get; set; }

    public OrganizationSponsorshipRequestModel() { }

    public OrganizationSponsorshipRequestModel(OrganizationSponsorshipData sponsorshipData)
    {
        SponsoringOrganizationUserId = sponsorshipData.SponsoringOrganizationUserId;
        FriendlyName = sponsorshipData.FriendlyName;
        OfferedToEmail = sponsorshipData.OfferedToEmail;
        PlanSponsorshipType = sponsorshipData.PlanSponsorshipType;
        LastSyncDate = sponsorshipData.LastSyncDate;
        ValidUntil = sponsorshipData.ValidUntil;
        ToDelete = sponsorshipData.ToDelete;
    }

    public OrganizationSponsorshipRequestModel(OrganizationSponsorship sponsorship)
    {
        SponsoringOrganizationUserId = sponsorship.SponsoringOrganizationUserId;
        FriendlyName = sponsorship.FriendlyName;
        OfferedToEmail = sponsorship.OfferedToEmail;
        PlanSponsorshipType = sponsorship.PlanSponsorshipType.GetValueOrDefault();
        LastSyncDate = sponsorship.LastSyncDate;
        ValidUntil = sponsorship.ValidUntil;
        ToDelete = sponsorship.ToDelete;
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
        };
    }
}
