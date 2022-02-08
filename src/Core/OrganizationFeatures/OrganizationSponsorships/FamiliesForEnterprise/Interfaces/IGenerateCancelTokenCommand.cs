using System.Threading.Tasks;
using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces
{
    public interface IGenerateCancelTokenCommand
    {
        Task<string> GenerateToken(string key, OrganizationSponsorship sponsorship);
    }
}
