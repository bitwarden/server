// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Vault.Models.Data;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;

namespace Bit.Infrastructure.EntityFramework.Vault.Repositories.Queries;

public class UserSecurityTasksByCipherIdsQuery : IQuery<UserCipherForTask>
{
    private readonly Guid _organizationId;
    private readonly IEnumerable<Guid> _cipherIds;

    public UserSecurityTasksByCipherIdsQuery(Guid organizationId, IEnumerable<Guid> cipherIds)
    {
        _organizationId = organizationId;
        _cipherIds = cipherIds;
    }

    public IQueryable<UserCipherForTask> Run(DatabaseContext dbContext)
    {
        var baseCiphers =
            from c in dbContext.Ciphers
            where _cipherIds.Contains(c.Id)
            join o in dbContext.Organizations
                on c.OrganizationId equals o.Id
            where o.Id == _organizationId && o.Enabled
            select c;

        var userPermissions =
            from c in baseCiphers
            join cc in dbContext.CollectionCiphers
                on c.Id equals cc.CipherId
            join cu in dbContext.CollectionUsers
                on cc.CollectionId equals cu.CollectionId
            join ou in dbContext.OrganizationUsers
                on cu.OrganizationUserId equals ou.Id
            where ou.OrganizationId == _organizationId
                && cu.Manage == true
            select new { ou.UserId, c.Id };

        var groupPermissions =
            from c in baseCiphers
            join cc in dbContext.CollectionCiphers
                on c.Id equals cc.CipherId
            join cg in dbContext.CollectionGroups
                on cc.CollectionId equals cg.CollectionId
            join gu in dbContext.GroupUsers
                on cg.GroupId equals gu.GroupId
            join ou in dbContext.OrganizationUsers
                on gu.OrganizationUserId equals ou.Id
            where ou.OrganizationId == _organizationId
                && cg.Manage == true
                && !userPermissions.Any(up => up.Id == c.Id && up.UserId == ou.UserId)
            select new { ou.UserId, c.Id };

        return userPermissions.Union(groupPermissions)
            .Join(
                dbContext.Users,
                p => p.UserId,
                u => u.Id,
                (p, u) => new { p.UserId, p.Id, u.Email }
            )
            .GroupBy(x => new { x.UserId, x.Email, x.Id })
            .Select(g => new UserCipherForTask
            {
                UserId = (Guid)g.Key.UserId,
                Email = g.Key.Email,
                CipherId = g.Key.Id
            })
            .OrderByDescending(x => x.Email);
    }
}
