using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories.Queries;

public class PolicyReadAcceptedOrConfirmedByUserIdQuery : IQuery<Policy>
{
    private readonly Guid _userId;

    public PolicyReadAcceptedOrConfirmedByUserIdQuery(Guid userId)
    {
        _userId = userId;
    }

    public IQueryable<Policy> Run(DatabaseContext dbContext)
    {
        var query = from p in dbContext.Policies
                    join ou in dbContext.OrganizationUsers
                        on p.OrganizationId equals ou.OrganizationId
                    join o in dbContext.Organizations
                        on ou.OrganizationId equals o.Id
                    where ou.UserId == _userId &&
                        (ou.Status == OrganizationUserStatusType.Confirmed
                         || ou.Status == OrganizationUserStatusType.Accepted)
                    select p;

        return query;
    }
}
