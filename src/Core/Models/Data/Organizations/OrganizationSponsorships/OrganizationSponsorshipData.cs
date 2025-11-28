// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Models.Data.Organizations.OrganizationSponsorships;

public class OrganizationSponsorshipData
{
    public OrganizationSponsorshipData() { }
    public OrganizationSponsorshipData(OrganizationSponsorship sponsorship)
    {
        SponsoringOrganizationUserId = sponsorship.SponsoringOrganizationUserId;
        SponsoredOrganizationId = sponsorship.SponsoredOrganizationId;
        FriendlyName = sponsorship.FriendlyName;
        OfferedToEmail = sponsorship.OfferedToEmail;
        PlanSponsorshipType = sponsorship.PlanSponsorshipType.GetValueOrDefault();
        LastSyncDate = sponsorship.LastSyncDate;
        ValidUntil = sponsorship.ValidUntil;
        ToDelete = sponsorship.ToDelete;
        IsAdminInitiated = sponsorship.IsAdminInitiated;
        Notes = sponsorship.Notes;
    }
    public Guid SponsoringOrganizationUserId { get; set; }
    public Guid? SponsoredOrganizationId { get; set; }
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
