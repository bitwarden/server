using Bit.Core.Enums.Provider;
using Bit.Core.Models.Data;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class UserReadPublicKeysByProviderUserIdsQuery : IQuery<ProviderUserPublicKey>
{
    private readonly Guid _providerId;
    private readonly IEnumerable<Guid> _ids;

    public UserReadPublicKeysByProviderUserIdsQuery(Guid providerId, IEnumerable<Guid> Ids)
    {
        _providerId = providerId;
        _ids = Ids;
    }

    public virtual IQueryable<ProviderUserPublicKey> Run(DatabaseContext dbContext)
    {
        var query = from pu in dbContext.ProviderUsers
                    join u in dbContext.Users
                        on pu.UserId equals u.Id
                    where _ids.Contains(pu.Id) &&
                        pu.Status == ProviderUserStatusType.Accepted &&
                        pu.ProviderId == _providerId
                    select new { pu, u };
        return query.Select(x => new ProviderUserPublicKey
        {
            Id = x.pu.Id,
            PublicKey = x.u.PublicKey,
        });
    }
}
