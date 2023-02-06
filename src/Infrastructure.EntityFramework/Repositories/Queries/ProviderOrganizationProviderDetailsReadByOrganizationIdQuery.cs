using Bit.Core.Models.Data;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class ProviderOrganizationProviderDetailsReadByOrganizationIdQuery : IQuery<ProviderOrganizationProviderDetails>
{
    private readonly Guid _organizationId;
    public ProviderOrganizationProviderDetailsReadByOrganizationIdQuery(Guid organizationId)
    {
        _organizationId = organizationId;
    }

    public IQueryable<ProviderOrganizationProviderDetails> Run(DatabaseContext dbContext)
    {
        var query = from po in dbContext.ProviderOrganizations
                    join p in dbContext.Providers
                        on po.ProviderId equals p.Id
                    join ou in dbContext.OrganizationUsers
                        on po.OrganizationId equals ou.OrganizationId
                    where po.OrganizationId == _organizationId
                    select new { po, p };
        return query.Select(x => new ProviderOrganizationProviderDetails()
        {
            Id = x.po.Id,
            ProviderId = x.po.ProviderId,
            OrganizationId = x.po.OrganizationId,
            ProviderName = x.p.Name,
            ProviderType = x.p.Type,
            ProviderStatus = x.p.Status,
            ProviderBillingEmail = x.p.BillingEmail
        });
    }
}
