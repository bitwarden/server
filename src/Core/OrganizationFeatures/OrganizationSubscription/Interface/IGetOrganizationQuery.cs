using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationSubscription.Interface;

public interface IGetOrganizationQuery
{
    Task<Organization> GetOrgById(Guid id);
}
