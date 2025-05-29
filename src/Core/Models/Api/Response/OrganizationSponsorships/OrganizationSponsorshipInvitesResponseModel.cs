using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationSponsorships;

namespace Bit.Core.Models.Api.Response.OrganizationSponsorships;

public class OrganizationSponsorshipInvitesResponseModel : ResponseModel
{
    public OrganizationSponsorshipInvitesResponseModel(OrganizationSponsorshipData sponsorshipData, string obj = "organizationSponsorship") : base(obj)
    {
        if (sponsorshipData == null)
        {
            throw new ArgumentNullException(nameof(sponsorshipData));
        }

        SponsoringOrganizationUserId = sponsorshipData.SponsoringOrganizationUserId;
        FriendlyName = sponsorshipData.FriendlyName;
        OfferedToEmail = sponsorshipData.OfferedToEmail;
        PlanSponsorshipType = sponsorshipData.PlanSponsorshipType;
        LastSyncDate = sponsorshipData.LastSyncDate;
        ValidUntil = sponsorshipData.ValidUntil;
        ToDelete = sponsorshipData.ToDelete;
        IsAdminInitiated = sponsorshipData.IsAdminInitiated;
        Notes = sponsorshipData.Notes;
        CloudSponsorshipRemoved = sponsorshipData.CloudSponsorshipRemoved;
    }

    public Guid SponsoringOrganizationUserId { get; set; }
    public string FriendlyName { get; set; }
    public string OfferedToEmail { get; set; }
    public PlanSponsorshipType PlanSponsorshipType { get; set; }
    public DateTime? LastSyncDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool ToDelete { get; set; }
    public bool IsAdminInitiated { get; set; }
    public string Notes { get; set; }
    public bool CloudSponsorshipRemoved { get; set; }
}
