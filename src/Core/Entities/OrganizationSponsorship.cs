using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Entities;

public class OrganizationSponsorship : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid? SponsoringOrganizationId { get; set; }
    public Guid SponsoringOrganizationUserId { get; set; }
    public Guid? SponsoredOrganizationId { get; set; }

    [MaxLength(256)]
    public string? FriendlyName { get; set; }

    [MaxLength(256)]
    public string? OfferedToEmail { get; set; }
    public PlanSponsorshipType? PlanSponsorshipType { get; set; }
    public DateTime? LastSyncDate { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool ToDelete { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
