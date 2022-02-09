using System;
using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Tokens;

namespace Bit.Core.Models.Business.Tokenables.SelfHosted
{
    public class SelfHostedOrganizationSponsorshipCancelTokenable : Tokenable, IBillingSyncTokenable
    {
        public const string ClearTextPrefix = "BWSHOrganizationSponsorshipCancel_";
        public const string TokenIdentifier = "SelfHostedOrganizationSponsorshipCancelToken";
        public string Identifier { get; set; } = TokenIdentifier;
        public Guid SponsoringOrganizationUserId { get; set; }
        public PlanSponsorshipType SponsorshipType { get; set; }
        public Guid OrganizationId { get; set; }
        public string BillingSyncKey { get; set; }

        public override bool Valid => Identifier == TokenIdentifier && SponsoringOrganizationUserId != default &&
            OrganizationId != default && !string.IsNullOrWhiteSpace(BillingSyncKey);

        [JsonConstructor]
        public SelfHostedOrganizationSponsorshipCancelTokenable() { }

        public SelfHostedOrganizationSponsorshipCancelTokenable(OrganizationSponsorship sponsorship, Guid cloudOrganizationId, string billingSyncKey)
        {
            if (string.IsNullOrWhiteSpace(billingSyncKey))
            {
                throw new ArgumentException("Invalid Billing Sync Key to create a token", nameof(billingSyncKey));
            }
            BillingSyncKey = billingSyncKey;

            if (cloudOrganizationId == default)
            {
                throw new ArgumentException("Invalid cloudOrganizationId to cancel a token", nameof(cloudOrganizationId));
            }
            OrganizationId = cloudOrganizationId;

            if (!sponsorship.SponsoringOrganizationUserId.HasValue || sponsorship.SponsoringOrganizationUserId == default)
            {
                throw new ArgumentException("Invalid OrganizationSponsorship to cancel a token, SponsoringOrganizationUserId is required", nameof(sponsorship));
            }
            SponsoringOrganizationUserId = sponsorship.SponsoringOrganizationUserId.Value;

            if (!sponsorship.PlanSponsorshipType.HasValue)
            {
                throw new ArgumentException("Invalid OrganizationSponsorship to cancel a token, PlanSponsorshipType is required", nameof(sponsorship));
            }
            SponsorshipType = sponsorship.PlanSponsorshipType.Value;
        }

        public bool IsValid(OrganizationSponsorship sponsorship, string expectedBillingSyncKey) =>
            sponsorship != null &&
            OrganizationId == sponsorship.SponsoringOrganizationId &&
            SponsoringOrganizationUserId == sponsorship.SponsoringOrganizationUserId &&
            sponsorship.PlanSponsorshipType.HasValue &&
            SponsorshipType == sponsorship.PlanSponsorshipType.Value &&
            !string.IsNullOrEmpty(expectedBillingSyncKey) &&
            expectedBillingSyncKey == BillingSyncKey;
    }
}
