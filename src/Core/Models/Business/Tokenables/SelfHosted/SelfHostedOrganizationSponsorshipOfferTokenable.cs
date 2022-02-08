using System;
using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Tokens;

namespace Bit.Core.Models.Business.Tokenables
{
    public class SelfHostedOrganizationSponsorshipOfferTokenable : OrganizationSponsorshipOfferTokenable, IBillingSyncTokenable
    {
        public const string ClearTextPrefix = "BWSHOrganizationSponsorship_";
        public const string TokenIdentifier = "SelfHostedOrganizationSponsorshipOfferToken";
        public string Identifier { get; set; } = TokenIdentifier;
        public Guid SponsoringOrganizationUserId { get; set; }
        public string SponsoringUserEmail { get; set; }
        public Guid OrganizationId { get; set; }
        public string BillingSyncKey { get; set; }

        public override bool Valid =>
            base.Valid &&
            Identifier == TokenIdentifier &&
            SponsoringOrganizationUserId != default &&
            OrganizationId != default;

        [JsonConstructor]
        public SelfHostedOrganizationSponsorshipOfferTokenable() { }

        public SelfHostedOrganizationSponsorshipOfferTokenable(OrganizationSponsorship sponsorship, Guid cloudOrganizationId, string billingSyncKey)
        {
            if (string.IsNullOrWhiteSpace(billingSyncKey))
            {
                throw new ArgumentException("Invalid Billing Sync Key to create a token", nameof(billingSyncKey));
            }
            BillingSyncKey = billingSyncKey;

            if (cloudOrganizationId == default)
            {
                throw new ArgumentException("Invalid cloudOrganizationId to create a token", nameof(cloudOrganizationId));
            }
            OrganizationId = cloudOrganizationId;

            if (!sponsorship.SponsoringOrganizationUserId.HasValue || sponsorship.SponsoringOrganizationUserId == default)
            {
                throw new ArgumentException("Invalid OrganizationSponsorship to create a token, SponsoringOrganizationUserId is required", nameof(sponsorship));
            }
            SponsoringOrganizationUserId = sponsorship.SponsoringOrganizationUserId.Value;
        }

        public bool IsValid(OrganizationSponsorship sponsorship, string currentUserEmail, string expectedBillingSyncKey) =>
            base.IsValid(sponsorship, currentUserEmail) &&
            OrganizationId == sponsorship.SponsoringOrganizationId &&
            SponsoringOrganizationUserId == sponsorship.SponsoringOrganizationUserId &&
            !string.IsNullOrEmpty(BillingSyncKey) &&
            !string.IsNullOrEmpty(expectedBillingSyncKey) &&
            expectedBillingSyncKey == BillingSyncKey;
    }
}
