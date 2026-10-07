using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.Queries;

public class CollectionReadCountByOrganizationIdQuery : IQuery<Collection>
{
    private readonly Guid _organizationId;

    public CollectionReadCountByOrganizationIdQuery(Guid organizationId)
    {
        _organizationId = organizationId;
    }

    public IQueryable<Collection> Run(DatabaseContext dbContext)
    {
        var query = from c in dbContext.Collections
                    where c.OrganizationId == _organizationId
                    select c;
        return query;
    }
}
