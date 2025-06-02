using Bit.Core.Entities;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class OrganizationUserReadByClaimedOrganizationDomainsQuery_vNext : IQuery<OrganizationUser>
{
    private readonly Guid _organizationId;

    public OrganizationUserReadByClaimedOrganizationDomainsQuery_vNext(Guid organizationId)
    {
        _organizationId = organizationId;
    }

    public IQueryable<OrganizationUser> Run(DatabaseContext dbContext)
    {
        var query = from ou in dbContext.OrganizationUsers
                    join u in dbContext.Users on ou.UserId equals u.Id
                    where ou.OrganizationId == _organizationId
                    let emailDomain = u.Email.Substring(u.Email.IndexOf('@') + 1)
                    where dbContext.OrganizationDomains
                          .Any(od => od.OrganizationId == _organizationId &&
                                     od.VerifiedDate != null &&
                                     emailDomain == od.DomainName)
                    select ou;

        return query;
    }
}
