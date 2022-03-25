using System;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api.Response.OrganizationSponsorships
{
    public class OrganizationSponsorshipResponseModel
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

        public OrganizationSponsorshipResponseModel(OrganizationSponsorshipData sponsorshipData)
        {
            SponsoringOrganizationUserId = SponsoringOrganizationUserId;
            SponsoredOrganizationId = SponsoredOrganizationId;
            FriendlyName = FriendlyName;
            OfferedToEmail = OfferedToEmail;
            PlanSponsorshipType = PlanSponsorshipType;
            LastSyncDate = LastSyncDate;
            ValidUntil = ValidUntil;
            ToDelete = ToDelete;
            CloudSponsorshipRemoved = CloudSponsorshipRemoved;
        }

        public OrganizationSponsorshipData ToOrganizationSponsorship()
        {
            return new OrganizationSponsorshipData
            {
                SponsoringOrganizationUserId = SponsoringOrganizationUserId,
                SponsoredOrganizationId = SponsoredOrganizationId,
                FriendlyName = FriendlyName,
                OfferedToEmail = OfferedToEmail,
                PlanSponsorshipType = PlanSponsorshipType,
                LastSyncDate = LastSyncDate,
                ValidUntil = ValidUntil,
                ToDelete = ToDelete,
                CloudSponsorshipRemoved = CloudSponsorshipRemoved
            };

        }
    }
}
