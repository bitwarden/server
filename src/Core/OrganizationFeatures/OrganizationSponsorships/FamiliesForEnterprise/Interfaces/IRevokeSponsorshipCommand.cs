using System.Threading.Tasks;
using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces
{
    public interface ICloudRevokeSponsorshipCommand
    {
        Task RevokeSponsorshipAsync(Organization sponsoredOrg, OrganizationSponsorship sponsorship);
    }

    public interface ISelfHostedRevokeSponsorshipCommand
    {
        Task RevokeSponsorshipAsync(OrganizationSponsorship sponsorship);
    }
}
