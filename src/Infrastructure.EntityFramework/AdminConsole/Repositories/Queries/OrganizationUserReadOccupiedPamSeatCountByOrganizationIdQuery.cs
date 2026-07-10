using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class OrganizationUserReadOccupiedPamSeatCountByOrganizationIdQuery : IQuery<OrganizationUser>
{
    private readonly Guid _organizationId;

    public OrganizationUserReadOccupiedPamSeatCountByOrganizationIdQuery(Guid organizationId)
    {
        _organizationId = organizationId;
    }

    public IQueryable<OrganizationUser> Run(DatabaseContext dbContext)
    {
        var query = from ou in dbContext.OrganizationUsers
                    where ou.OrganizationId == _organizationId
                          && (ou.Status == OrganizationUserStatusType.Invited ||
                              ou.Status == OrganizationUserStatusType.Accepted ||
                              ou.Status == OrganizationUserStatusType.Confirmed)
                          && ou.AccessPam == true
                    select ou;
        return query;
    }
}
