using System.Data.Common;
using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;
using Microsoft.EntityFrameworkCore;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class OrganizationUserReadOccupiedSmSeatCountByOrganizationIdQuery : IQuery<OrganizationUser>
{
    private readonly Guid _organizationId;
    private readonly DbTransaction? _dbTransaction;

    public OrganizationUserReadOccupiedSmSeatCountByOrganizationIdQuery(Guid organizationId)
    {
        _organizationId = organizationId;
    }

    public OrganizationUserReadOccupiedSmSeatCountByOrganizationIdQuery(Guid organizationId, DbTransaction dbTransaction)
    {
        _organizationId = organizationId;
        _dbTransaction = dbTransaction;
    }

    public IQueryable<OrganizationUser> Run(DatabaseContext dbContext)
    {
        if (_dbTransaction is not null)
        {
            dbContext.Database.UseTransaction(_dbTransaction);
        }

        var query = from ou in dbContext.OrganizationUsers
                    where ou.OrganizationId == _organizationId && ou.Status >= OrganizationUserStatusType.Invited && ou.AccessSecretsManager == true
                    select ou;
        return query;
    }
}
