using System;
using System.Text.Json.Serialization;
using Bit.Core.Entities;

namespace Bit.Core.Models.Business.Tokenables.Cloud
{
    public class CloudOrganizationSponsorshipOfferTokenable : OrganizationSponsorshipOfferTokenable
    {
        public const string ClearTextPrefix = "BWOrganizationSponsorship_";
        public const string DataProtectorPurpose = "OrganizationSponsorshipDataProtector";
        public const string TokenIdentifier = "OrganizationSponsorshipOfferToken";
        public string Identifier { get; set; } = TokenIdentifier;
        public Guid Id { get; set; }

        public override bool Valid => base.Valid && Identifier == TokenIdentifier && Id != default;

        [JsonConstructor]
        public CloudOrganizationSponsorshipOfferTokenable() { }

        public CloudOrganizationSponsorshipOfferTokenable(OrganizationSponsorship sponsorship) : base(sponsorship)
        {
            if (sponsorship.Id == default)
            {
                throw new ArgumentException("Invalid OrganizationSponsorship to create a token, Id is required", nameof(sponsorship));
            }
            Id = sponsorship.Id;
        }

        public override bool IsValid(OrganizationSponsorship sponsorship, string currentUserEmail) =>
            base.IsValid(sponsorship, currentUserEmail) && Id == sponsorship.Id;
    }
}
