using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.Queries;

public class ProviderOrganizationReadByUserIdQuery : IQuery<ProviderOrganizationProviderDetails>
{
    private readonly Guid _userId;

    public ProviderOrganizationReadByUserIdQuery(Guid userId)
    {
        _userId = userId;
    }

    public IQueryable<ProviderOrganizationProviderDetails> Run(DatabaseContext dbContext)
    {
        var query =
            from po in dbContext.ProviderOrganizations
            join ou in dbContext.OrganizationUsers on po.OrganizationId equals ou.OrganizationId
            join p in dbContext.Providers on po.ProviderId equals p.Id
            where ou.UserId == _userId
            select new ProviderOrganizationProviderDetails
            {
                Id = po.Id,
                OrganizationId = po.OrganizationId,
                ProviderId = po.ProviderId,
                ProviderName = p.Name,
                ProviderType = p.Type,
            };
        return query;
    }
}
