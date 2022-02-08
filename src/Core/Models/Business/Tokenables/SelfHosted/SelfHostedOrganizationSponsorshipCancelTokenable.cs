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
        public Guid SponsoringOrganizationId { get; set; }
        public PlanSponsorshipType SponsorshipType { get; set; }
        public Guid OrganizationId { get; set; }
        public string BillingSyncKey { get; set; }

        public override bool Valid => Identifier == TokenIdentifier && SponsoringOrganizationUserId != default &&
            SponsoringOrganizationId != default && OrganizationId != default && !string.IsNullOrWhiteSpace(BillingSyncKey);


        [JsonConstructor]
        public SelfHostedOrganizationSponsorshipCancelTokenable() { }

        public SelfHostedOrganizationSponsorshipCancelTokenable(OrganizationSponsorship sponsorship)
        {
            if (!sponsorship.SponsoringOrganizationId.HasValue || sponsorship.SponsoringOrganizationId == default)
            {
                throw new ArgumentException("Invalid OrganizationSponsorship to cancel a token, SponsoringOrganizationId is required", nameof(sponsorship));
            }
            SponsoringOrganizationId = sponsorship.SponsoringOrganizationId.Value;

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

        public bool IsValid(OrganizationSponsorship sponsorship) =>
            sponsorship != null &&
            SponsoringOrganizationId == sponsorship.SponsoringOrganizationId &&
            SponsoringOrganizationUserId == sponsorship.SponsoringOrganizationUserId &&
            sponsorship.PlanSponsorshipType.HasValue &&
            SponsorshipType == sponsorship.PlanSponsorshipType.Value &&
            !string.IsNullOrWhiteSpace(sponsorship.OfferedToEmail);

        public bool IsValid(OrganizationSponsorship sponsorship, string currentUserEmail, string expectedBillingSyncKey) =>
            !string.IsNullOrEmpty(BillingSyncKey) &&
            !string.IsNullOrEmpty(expectedBillingSyncKey) &&
            expectedBillingSyncKey == BillingSyncKey &&
            IsValid(sponsorship);
    }
}
