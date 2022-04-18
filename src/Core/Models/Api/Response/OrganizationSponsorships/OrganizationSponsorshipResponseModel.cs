using System;
using System.Text.Json.Serialization;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationSponsorships;

namespace Bit.Core.Models.Api.Response.OrganizationSponsorships
{
    public class OrganizationSponsorshipResponseModel
    {
        [JsonPropertyName("SponsoringOrganizationUserId")]
        public Guid SponsoringOrganizationUserId { get; set; }
        [JsonPropertyName("FriendlyName")]
        public string FriendlyName { get; set; }
        [JsonPropertyName("OfferedToEmail")]
        public string OfferedToEmail { get; set; }
        [JsonPropertyName("PlanSponsorshipType")]
        public PlanSponsorshipType PlanSponsorshipType { get; set; }
        [JsonPropertyName("LastSyncDate")]
        public DateTime? LastSyncDate { get; set; }
        [JsonPropertyName("ValidUntil")]
        public DateTime? ValidUntil { get; set; }
        [JsonPropertyName("ToDelete")]
        public bool ToDelete { get; set; }

        [JsonPropertyName("CloudSponsorshipRemoved")]
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
                CloudSponsorshipRemoved = CloudSponsorshipRemoved
            };

        }
    }
}
