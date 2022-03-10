using System;
using Bit.Core.Enums;

namespace Bit.Core.Models.Api.Request.OrganizationSponsorships
{
    public class OrganizationSponsorshipModel
    {
        public Guid? SponsoringOrganizationUserId { get; set; }
        public Guid? SponsoredOrganizationId { get; set; }
        public string FriendlyName { get; set; }
        public string OfferedToEmail { get; set; }
        public PlanSponsorshipType? PlanSponsorshipType { get; set; }
        public DateTime? LastSyncDate { get; set; }
        public DateTime? ValidUntil { get; set; }
    }
}
