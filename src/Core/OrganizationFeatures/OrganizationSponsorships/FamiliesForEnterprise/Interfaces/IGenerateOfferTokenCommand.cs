using System.Threading.Tasks;
using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces
{
    public interface IGenerateOfferTokenCommand
    {
        string GenerateToken(string key, string sponsoringUserEmail, OrganizationSponsorship sponsorship);
    }
}
