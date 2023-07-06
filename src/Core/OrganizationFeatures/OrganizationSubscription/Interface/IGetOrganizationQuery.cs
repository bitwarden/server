using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSmSubscription.Interface;

public interface IGetOrganizationQuery
{
    Task<Organization> GetOrgById(Guid id);
}
