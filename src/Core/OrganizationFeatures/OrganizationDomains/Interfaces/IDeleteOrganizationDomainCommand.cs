namespace Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface IDeleteOrganizationDomainCommand
{
    Task DeleteAsync(Guid id);
}
