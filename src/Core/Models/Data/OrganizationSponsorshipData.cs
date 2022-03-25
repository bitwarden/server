using System;
using Bit.Core.Enums;

namespace Bit.Core.Models.Data
{
    public class OrganizationSponsorshipData
    {
        public Guid? SponsoringOrganizationUserId { get; set; }
        public Guid? SponsoredOrganizationId { get; set; }
        public string FriendlyName { get; set; }
        public string OfferedToEmail { get; set; }
        public PlanSponsorshipType? PlanSponsorshipType { get; set; }
        public DateTime? LastSyncDate { get; set; }
        public DateTime? ValidUntil { get; set; }
        public bool ToDelete { get; set; }

        public bool CloudSponsorshipRemoved { get; set; }

    }
}
