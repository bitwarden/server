using Bit.Core.Enums.Provider;
using Bit.Core.Models.Data;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class ProviderUserProviderDetailsReadByUserIdStatusQuery : IQuery<ProviderUserProviderDetails>
{
    private readonly Guid _userId;
    private readonly ProviderUserStatusType? _status;
    public ProviderUserProviderDetailsReadByUserIdStatusQuery(Guid userId, ProviderUserStatusType? status)
    {
        _userId = userId;
        _status = status;
    }

    public IQueryable<ProviderUserProviderDetails> Run(DatabaseContext dbContext)
    {
        var query = from pu in dbContext.ProviderUsers
                    join p in dbContext.Providers
                        on pu.ProviderId equals p.Id into p_g
                    from p in p_g.DefaultIfEmpty()
                    where pu.UserId == _userId && p.Status != ProviderStatusType.Pending && (_status == null || pu.Status == _status)
                    select new { pu, p };
        return query.Select(x => new ProviderUserProviderDetails()
        {
            UserId = x.pu.UserId,
            ProviderId = x.pu.ProviderId,
            Name = x.p.Name,
            Key = x.pu.Key,
            Status = x.pu.Status,
            Type = x.pu.Type,
            Enabled = x.p.Enabled,
            Permissions = x.pu.Permissions,
            UseEvents = x.p.UseEvents,
            ProviderStatus = x.p.Status,
        });
    }
}
