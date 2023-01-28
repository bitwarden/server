using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;

public interface IValidateBillingSyncKeyCommand
{
    Task<bool> ValidateBillingSyncKeyAsync(Organization organization, string billingSyncKey);
}
