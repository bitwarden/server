using System.Text.Json.Serialization;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Tokens;

namespace Bit.Core.Models.Business.Tokenables;

public class OrganizationSponsorshipOfferTokenable : Tokenable
{
    public const string ClearTextPrefix = "BWOrganizationSponsorship_";
    public const string DataProtectorPurpose = "OrganizationSponsorshipDataProtector";
    public const string TokenIdentifier = "OrganizationSponsorshipOfferToken";
    public string Identifier { get; set; } = TokenIdentifier;
    public Guid Id { get; set; }
    public PlanSponsorshipType SponsorshipType { get; set; }
    public string Email { get; set; }

    public override bool Valid =>
        !string.IsNullOrWhiteSpace(Email) && Identifier == TokenIdentifier && Id != default;

    [JsonConstructor]
    public OrganizationSponsorshipOfferTokenable() { }

    public OrganizationSponsorshipOfferTokenable(OrganizationSponsorship sponsorship)
    {
        if (string.IsNullOrWhiteSpace(sponsorship.OfferedToEmail))
        {
            throw new ArgumentException(
                "Invalid OrganizationSponsorship to create a token, OfferedToEmail is required",
                nameof(sponsorship)
            );
        }
        Email = sponsorship.OfferedToEmail;

        if (!sponsorship.PlanSponsorshipType.HasValue)
        {
            throw new ArgumentException(
                "Invalid OrganizationSponsorship to create a token, PlanSponsorshipType is required",
                nameof(sponsorship)
            );
        }
        SponsorshipType = sponsorship.PlanSponsorshipType.Value;

        if (sponsorship.Id == default)
        {
            throw new ArgumentException(
                "Invalid OrganizationSponsorship to create a token, Id is required",
                nameof(sponsorship)
            );
        }
        Id = sponsorship.Id;
    }

    public bool IsValid(OrganizationSponsorship sponsorship, string currentUserEmail) =>
        sponsorship != null
        && sponsorship.PlanSponsorshipType.HasValue
        && SponsorshipType == sponsorship.PlanSponsorshipType.Value
        && Id == sponsorship.Id
        && !string.IsNullOrWhiteSpace(sponsorship.OfferedToEmail)
        && Email.Equals(currentUserEmail, StringComparison.InvariantCultureIgnoreCase)
        && Email.Equals(sponsorship.OfferedToEmail, StringComparison.InvariantCultureIgnoreCase);
}
