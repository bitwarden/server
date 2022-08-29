using Bit.Core.Enums.Provider;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class ProviderUserReadCountByOnlyOwnerQuery : IQuery<ProviderUser>
{
    private readonly Guid _userId;

    public ProviderUserReadCountByOnlyOwnerQuery(Guid userId)
    {
        _userId = userId;
    }

    public IQueryable<ProviderUser> Run(DatabaseContext dbContext)
    {
        var owners = from pu in dbContext.ProviderUsers
                     where pu.Type == ProviderUserType.ProviderAdmin &&
                         pu.Status == ProviderUserStatusType.Confirmed
                     group pu by pu.ProviderId into g
                     select new
                     {
                         ProviderUser = g.Select(x => new { x.UserId, x.Id }).FirstOrDefault(),
                         ConfirmedOwnerCount = g.Count(),
                     };

        var query = from owner in owners
                    join pu in dbContext.ProviderUsers
                        on owner.ProviderUser.Id equals pu.Id
                    where owner.ProviderUser.UserId == _userId &&
                        owner.ConfirmedOwnerCount == 1
                    select pu;

        return query;
    }
}
