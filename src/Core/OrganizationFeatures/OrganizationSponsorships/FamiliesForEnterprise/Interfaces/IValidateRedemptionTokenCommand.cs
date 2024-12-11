using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;

public interface IValidateRedemptionTokenCommand
{
    Task<(bool valid, OrganizationSponsorship sponsorship)> ValidateRedemptionTokenAsync(
        string encryptedToken,
        string sponsoredUserEmail
    );
}
