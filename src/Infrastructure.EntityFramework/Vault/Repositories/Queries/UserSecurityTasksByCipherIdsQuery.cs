using Bit.Core.Vault.Models.Data;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.Vault.Repositories.Queries;

public class UserSecurityTasksByCipherIdsQuery : IQuery<UserSecurityTasksCount>
{
    private readonly Guid _organizationId;
    private readonly IEnumerable<Guid> _cipherIds;

    public UserSecurityTasksByCipherIdsQuery(Guid organizationId, IEnumerable<Guid> cipherIds)
    {
        _organizationId = organizationId;
        _cipherIds = cipherIds;
    }

    public IQueryable<UserSecurityTasksCount> Run(DatabaseContext dbContext)
    {
        var userPermissions =
            from c in dbContext.Ciphers.Where(c => _cipherIds.Contains(c.Id))
            join cc in dbContext.CollectionCiphers on c.Id equals cc.CipherId
            join cu in dbContext.CollectionUsers on cc.CollectionId equals cu.CollectionId
            join ou in dbContext.OrganizationUsers on cu.OrganizationUserId equals ou.Id
            where ou.OrganizationId == _organizationId && cu.Manage == true
            select new { ou.UserId, c.Id };

        var groupPermissions =
            from c in dbContext.Ciphers.Where(c => _cipherIds.Contains(c.Id))
            join cc in dbContext.CollectionCiphers on c.Id equals cc.CipherId
            join cg in dbContext.CollectionGroups on cc.CollectionId equals cg.CollectionId
            join gu in dbContext.GroupUsers on cg.GroupId equals gu.GroupId
            join ou in dbContext.OrganizationUsers on gu.OrganizationUserId equals ou.Id
            where ou.OrganizationId == _organizationId && cg.Manage == true
            select new { ou.UserId, c.Id };

        return userPermissions.Union(groupPermissions)
            .GroupBy(x => x.UserId)
            .Select(g => new UserSecurityTasksCount
            {
                UserId = (Guid)g.Key,
                TaskCount = g.Select(x => x.Id).Distinct().Count()
            });
    }
}
