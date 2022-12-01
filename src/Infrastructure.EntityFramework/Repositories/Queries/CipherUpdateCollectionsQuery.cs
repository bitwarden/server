using Bit.Core.Entities;
using Bit.Core.Enums;
using CollectionCipher = Bit.Infrastructure.EntityFramework.Models.CollectionCipher;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class CipherUpdateCollectionsQuery : IQuery<CollectionCipher>
{
    private readonly Cipher _cipher;
    private readonly IEnumerable<Guid> _collectionIds;

    public CipherUpdateCollectionsQuery(Cipher cipher, IEnumerable<Guid> collectionIds)
    {
        _cipher = cipher;
        _collectionIds = collectionIds;
    }

    public virtual IQueryable<CollectionCipher> Run(DatabaseContext dbContext)
    {
        if (!_cipher.OrganizationId.HasValue || !_collectionIds.Any())
        {
            return null;
        }

        var availibleCollections = !_cipher.UserId.HasValue ?

            from c in dbContext.Collections
            where c.OrganizationId == _cipher.OrganizationId
            select c.Id :

            from c in dbContext.Collections

            join o in dbContext.Organizations
                on c.OrganizationId equals o.Id

            join ou in dbContext.OrganizationUsers
                on new { OrganizationId = o.Id, _cipher.UserId } equals new { ou.OrganizationId, ou.UserId }

            join cu in dbContext.CollectionUsers
                on new { ou.AccessAll, CollectionId = c.Id, OrganizationUserId = ou.Id } equals
                   new { AccessAll = false, cu.CollectionId, cu.OrganizationUserId } into cu_g
            from cu in cu_g.DefaultIfEmpty()

            join gu in dbContext.GroupUsers
                on new { CollectionId = (Guid?)cu.CollectionId, ou.AccessAll, OrganizationUserId = ou.Id } equals
                   new { CollectionId = (Guid?)null, AccessAll = false, gu.OrganizationUserId } into gu_g
            from gu in gu_g.DefaultIfEmpty()

            join g in dbContext.Groups
                on gu.GroupId equals g.Id into g_g
            from g in g_g.DefaultIfEmpty()

            join cg in dbContext.CollectionGroups
                on new { g.AccessAll, CollectionId = c.Id, gu.GroupId } equals
                   new { AccessAll = false, cg.CollectionId, cg.GroupId } into cg_g
            from cg in cg_g.DefaultIfEmpty()

            where o.Id == _cipher.OrganizationId &&
                o.Enabled &&
                ou.Status == OrganizationUserStatusType.Confirmed &&
                (ou.AccessAll || !cu.ReadOnly || g.AccessAll || !cg.ReadOnly)
            select c.Id;

        if (!availibleCollections.Any())
        {
            return null;
        }

        var query = from c in availibleCollections
                    select new CollectionCipher { CollectionId = c, CipherId = _cipher.Id };
        return query;
    }
}
