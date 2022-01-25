using System;
using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Tokens;

namespace Bit.Core.Models.Business.Tokenables
{
    public class OrganizationSponsorshipOfferTokenable : Tokenable
    {
        public const string ClearTextPrefix = "BWF4EOrganizationSponsorship_";
        public const string DataProtectorPurpose = "EmergencyAccessServiceDataProtector";
        public const string TokenIdentifier = "FamiliesForEnterpriseToken";
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
            Id = sponsorship.Id;
            Email = sponsorship.OfferedToEmail;
            if (!sponsorship.PlanSponsorshipType.HasValue)
            {
                throw new ArgumentException("Invalid OrganizationSponsorship to crate a token, PlanSponsorshipType is required", nameof(sponsorship));
            }
            SponsorshipType = sponsorship.PlanSponsorshipType.Value;
        }

        public bool IsValid(OrganizationSponsorship sponsorship) =>
            Id == sponsorship.Id &&
            sponsorship.PlanSponsorshipType.HasValue &&
            SponsorshipType == sponsorship.PlanSponsorshipType.Value &&
            Email.Equals(sponsorship.OfferedToEmail, StringComparison.InvariantCultureIgnoreCase);

    }
}
