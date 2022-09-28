using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class OrganizationUserReadCountByOnlyOwnerQuery : IQuery<OrganizationUser>
{
    private readonly Guid _userId;

    public OrganizationUserReadCountByOnlyOwnerQuery(Guid userId)
    {
        _userId = userId;
    }

    public IQueryable<OrganizationUser> Run(DatabaseContext dbContext)
    {
        var owners = from ou in dbContext.OrganizationUsers
                     where ou.Type == OrganizationUserType.Owner &&
                         ou.Status == OrganizationUserStatusType.Confirmed
                     group ou by ou.OrganizationId into g
                     select new
                     {
                         OrgUser = g.Select(x => new { x.UserId, x.Id }).FirstOrDefault(),
                         ConfirmedOwnerCount = g.Count(),
                     };

        var query = from owner in owners
                    join ou in dbContext.OrganizationUsers
                        on owner.OrgUser.Id equals ou.Id
                    where owner.OrgUser.UserId == _userId &&
                        owner.ConfirmedOwnerCount == 1
                    select ou;

        return query;
    }
}
