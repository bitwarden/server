using System.Threading.Tasks;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces
{
    public interface IValidateRedemptionTokenCommand
    {
        Task<bool> ValidateRedemptionTokenAsync(string encryptedToken, string sponsoredUserEmail);
    }
}
