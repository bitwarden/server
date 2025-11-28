using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class UserBumpAccountRevisionDateByOrganizationIdQuery : IQuery<User>
{
    private readonly Guid _organizationId;

    public UserBumpAccountRevisionDateByOrganizationIdQuery(Guid organizationId)
    {
        _organizationId = organizationId;
    }

    public IQueryable<User> Run(DatabaseContext dbContext)
    {
        var query = from u in dbContext.Users
                    join ou in dbContext.OrganizationUsers
                        on u.Id equals ou.UserId
                    where ou.OrganizationId == _organizationId &&
                        ou.Status == OrganizationUserStatusType.Confirmed
                    select u;

        return query;
    }
}
