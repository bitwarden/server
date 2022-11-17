using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class OrganizationUserReadCountByMinimumStatusOrganizationIdQuery: IQuery<OrganizationUser>
{
    private readonly Guid _organizationId;
    private readonly OrganizationUserStatusType _minimumStatus;

    public OrganizationUserReadCountByMinimumStatusOrganizationIdQuery(Guid organizationId,
        OrganizationUserStatusType minimumStatus)
    {
        _organizationId = organizationId;
        _minimumStatus = minimumStatus;
    }

    public IQueryable<OrganizationUser> Run(DatabaseContext dbContext)
    {
        var query = from ou in dbContext.OrganizationUsers
                    where ou.OrganizationId == _organizationId && ou.Status >= _minimumStatus
                    select ou;
        return query;
    }
}
