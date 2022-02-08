using System;
using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Tokens;

namespace Bit.Core.Models.Business.Tokenables
{
    public abstract class OrganizationSponsorshipOfferTokenable : Tokenable
    {
        public PlanSponsorshipType SponsorshipType { get; set; }
        public string Email { get; set; }

        public override bool Valid => !string.IsNullOrWhiteSpace(Email);

        [JsonConstructor]
        public OrganizationSponsorshipOfferTokenable() { }

        public OrganizationSponsorshipOfferTokenable(OrganizationSponsorship sponsorship)
        {
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

        public virtual bool IsValid(OrganizationSponsorship sponsorship, string currentUserEmail) =>
            sponsorship != null &&
            sponsorship.PlanSponsorshipType.HasValue &&
            SponsorshipType == sponsorship.PlanSponsorshipType.Value &&
            !string.IsNullOrWhiteSpace(sponsorship.OfferedToEmail) &&
            Email.Equals(currentUserEmail, StringComparison.InvariantCultureIgnoreCase) &&
            Email.Equals(sponsorship.OfferedToEmail, StringComparison.InvariantCultureIgnoreCase);
    }
}
