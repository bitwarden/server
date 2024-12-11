using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class OrganizationUserReadCountByFreeOrganizationAdminUserQuery : IQuery<OrganizationUser>
{
    private readonly Guid _userId;

    public OrganizationUserReadCountByFreeOrganizationAdminUserQuery(Guid userId)
    {
        _userId = userId;
    }

    public IQueryable<OrganizationUser> Run(DatabaseContext dbContext)
    {
        var query =
            from ou in dbContext.OrganizationUsers
            join o in dbContext.Organizations on ou.OrganizationId equals o.Id
            where
                ou.UserId == _userId
                && (ou.Type == OrganizationUserType.Owner || ou.Type == OrganizationUserType.Admin)
                && o.PlanType == PlanType.Free
                && ou.Status == OrganizationUserStatusType.Confirmed
            select ou;

        return query;
    }
}
