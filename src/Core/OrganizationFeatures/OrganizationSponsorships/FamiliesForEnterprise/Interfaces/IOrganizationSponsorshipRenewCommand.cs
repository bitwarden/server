namespace Bit.Core.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise.Interfaces;

public interface IOrganizationSponsorshipRenewCommand
{
    Task UpdateExpirationDateAsync(Guid organizationId, DateTime expireDate);
}
