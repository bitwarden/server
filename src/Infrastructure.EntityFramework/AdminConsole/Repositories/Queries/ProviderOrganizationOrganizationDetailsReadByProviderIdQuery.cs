using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.Queries;

public class ProviderOrganizationOrganizationDetailsReadByProviderIdQuery
    : IQuery<ProviderOrganizationOrganizationDetails>
{
    private readonly Guid _providerId;

    public ProviderOrganizationOrganizationDetailsReadByProviderIdQuery(Guid providerId)
    {
        _providerId = providerId;
    }

    public IQueryable<ProviderOrganizationOrganizationDetails> Run(DatabaseContext dbContext)
    {
        var query =
            from po in dbContext.ProviderOrganizations
            join o in dbContext.Organizations on po.OrganizationId equals o.Id
            join ou in dbContext.OrganizationUsers on po.OrganizationId equals ou.OrganizationId
            where po.ProviderId == _providerId
            select new { po, o };
        return query.Select(x => new ProviderOrganizationOrganizationDetails()
        {
            Id = x.po.Id,
            ProviderId = x.po.ProviderId,
            OrganizationId = x.po.OrganizationId,
            OrganizationName = x.o.Name,
            Key = x.po.Key,
            Settings = x.po.Settings,
            CreationDate = x.po.CreationDate,
            RevisionDate = x.po.RevisionDate,
            UserCount = x.o.OrganizationUsers.Count(ou =>
                ou.Status == Core.Enums.OrganizationUserStatusType.Confirmed
            ),
            OccupiedSeats = x.o.OrganizationUsers.Count(ou => ou.Status >= 0),
            Seats = x.o.Seats,
            Plan = x.o.Plan,
            Status = x.o.Status,
        });
    }
}
