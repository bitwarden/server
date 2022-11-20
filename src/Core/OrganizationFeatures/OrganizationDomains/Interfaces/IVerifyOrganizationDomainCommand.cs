namespace Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface IVerifyOrganizationDomainCommand
{
    Task<bool> VerifyOrganizationDomain(Guid id);
}
