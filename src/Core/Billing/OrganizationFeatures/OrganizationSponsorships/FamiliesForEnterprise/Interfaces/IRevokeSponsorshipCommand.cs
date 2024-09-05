using Bit.Core.Entities;

namespace Bit.Core.Billing.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;

public interface IRevokeSponsorshipCommand
{
    Task RevokeSponsorshipAsync(OrganizationSponsorship sponsorship);
}
