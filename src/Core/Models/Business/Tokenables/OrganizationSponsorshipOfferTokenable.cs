using System;
using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Tokens;

namespace Bit.Core.Models.Business.Tokenables
{
    public class OrganizationSponsorshipOfferTokenable : Tokenable
    {
        public const string ClearTextPrefix = "BWOrganizationSponsorship_";
        public const string DataProtectorPurpose = "EmergencyAccessServiceDataProtector";
        public const string TokenIdentifier = "OrganizationSponsorshipOfferToken";
        public string Identifier { get; set; } = TokenIdentifier;
        public Guid Id { get; set; }
        public PlanSponsorshipType SponsorshipType { get; set; }
        public string Email { get; set; }

        public override bool Valid => Identifier == TokenIdentifier && Id != default &&
            !string.IsNullOrWhiteSpace(Email);

        [JsonConstructor]
        public OrganizationSponsorshipOfferTokenable() { }

        public OrganizationSponsorshipOfferTokenable(OrganizationSponsorship sponsorship)
        {
            if (sponsorship.Id == null || sponsorship.Id == default)
            {
                throw new ArgumentException("Invalid OrganizationSponsorship to create a token, Id is required", nameof(sponsorship));
            }
            Id = sponsorship.Id;

            if (string.IsNullOrWhiteSpace(sponsorship.OfferedToEmail))
            {
                throw new ArgumentException("Invalid OrganizationSponsorship to create a token, OfferedToEmail is required", nameof(sponsorship));
            }
            Email = sponsorship.OfferedToEmail;

            if (!sponsorship.PlanSponsorshipType.HasValue)
            {
                throw new ArgumentException("Invalid OrganizationSponsorship to create a token, PlanSponsorshipType is required", nameof(sponsorship));
            }
            SponsorshipType = sponsorship.PlanSponsorshipType.Value;
        }

        public bool IsValid(OrganizationSponsorship sponsorship, string currentUserEmail) =>
            sponsorship != null &&
            Id == sponsorship.Id &&
            sponsorship.PlanSponsorshipType.HasValue &&
            SponsorshipType == sponsorship.PlanSponsorshipType.Value &&
            !string.IsNullOrWhiteSpace(sponsorship.OfferedToEmail) &&
            Email.Equals(currentUserEmail, StringComparison.InvariantCultureIgnoreCase) &&
            Email.Equals(sponsorship.OfferedToEmail, StringComparison.InvariantCultureIgnoreCase);

    }
}
