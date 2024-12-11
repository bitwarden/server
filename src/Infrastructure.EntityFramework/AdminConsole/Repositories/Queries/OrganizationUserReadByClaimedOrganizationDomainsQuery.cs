using Bit.Core.Entities;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class OrganizationUserReadByClaimedOrganizationDomainsQuery : IQuery<OrganizationUser>
{
    private readonly Guid _organizationId;

    public OrganizationUserReadByClaimedOrganizationDomainsQuery(Guid organizationId)
    {
        _organizationId = organizationId;
    }

    public IQueryable<OrganizationUser> Run(DatabaseContext dbContext)
    {
        var query =
            from ou in dbContext.OrganizationUsers
            join u in dbContext.Users on ou.UserId equals u.Id
            where
                ou.OrganizationId == _organizationId
                && dbContext.OrganizationDomains.Any(od =>
                    od.OrganizationId == _organizationId
                    && od.VerifiedDate != null
                    && u.Email.ToLower().EndsWith("@" + od.DomainName.ToLower())
                )
            select ou;

        return query;
    }
}
