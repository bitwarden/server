using Bit.Core.Entities;
using Bit.Core.Enums;
using User = Bit.Infrastructure.EntityFramework.Models.User;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class UserBumpAccountRevisionDateByCipherIdQuery : IQuery<User>
{
    private readonly Cipher _cipher;

    public UserBumpAccountRevisionDateByCipherIdQuery(Cipher cipher)
    {
        _cipher = cipher;
    }

    public IQueryable<User> Run(DatabaseContext dbContext)
    {
        var query = from u in dbContext.Users

                    join ou in dbContext.OrganizationUsers
                        on u.Id equals ou.UserId

                    join collectionCipher in dbContext.CollectionCiphers
                        on _cipher.Id equals collectionCipher.CipherId into cc_g
                    from cc in cc_g.DefaultIfEmpty()

                    join collectionUser in dbContext.CollectionUsers
                        on new { ou.AccessAll, OrganizationUserId = ou.Id, cc.CollectionId } equals
                           new { AccessAll = false, collectionUser.OrganizationUserId, collectionUser.CollectionId } into cu_g
                    from cu in cu_g.DefaultIfEmpty()

                    join groupUser in dbContext.GroupUsers
                        on new { CollectionId = (Guid?)cu.CollectionId, ou.AccessAll, OrganizationUserId = ou.Id } equals
                           new { CollectionId = (Guid?)null, AccessAll = false, groupUser.OrganizationUserId } into gu_g
                    from gu in gu_g.DefaultIfEmpty()

                    join grp in dbContext.Groups
                        on gu.GroupId equals grp.Id into g_g
                    from g in g_g.DefaultIfEmpty()

                    join collectionGroup in dbContext.CollectionGroups
                        on new { g.AccessAll, gu.GroupId, cc.CollectionId } equals
                           new { AccessAll = false, collectionGroup.GroupId, collectionGroup.CollectionId } into cg_g
                    from cg in cg_g.DefaultIfEmpty()

                    where ou.OrganizationId == _cipher.OrganizationId &&
                            ou.Status == OrganizationUserStatusType.Confirmed &&
                            (cu.CollectionId != null ||
                            cg.CollectionId != null ||
                            ou.AccessAll ||
                            g.AccessAll)
                    select u;
        return query;
    }
}
