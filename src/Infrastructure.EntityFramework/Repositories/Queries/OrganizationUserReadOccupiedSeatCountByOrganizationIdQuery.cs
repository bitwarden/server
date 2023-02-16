using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class OrganizationUserReadOccupiedSeatCountByOrganizationIdQuery : IQuery<OrganizationUser>
{
    private readonly Guid _organizationId;

    public OrganizationUserReadOccupiedSeatCountByOrganizationIdQuery(Guid organizationId)
    {
        _organizationId = organizationId;
    }

    public IQueryable<OrganizationUser> Run(DatabaseContext dbContext)
    {
        var query = from ou in dbContext.OrganizationUsers
                    where ou.OrganizationId == _organizationId && ou.Status >= OrganizationUserStatusType.Invited
                    select ou;
        return query;
    }
}
