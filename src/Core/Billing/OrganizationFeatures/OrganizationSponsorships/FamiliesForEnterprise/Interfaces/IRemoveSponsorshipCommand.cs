using Bit.Core.Entities;

namespace Bit.Core.Billing.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;

public interface IRemoveSponsorshipCommand
{
    Task RemoveSponsorshipAsync(OrganizationSponsorship sponsorship);
}
