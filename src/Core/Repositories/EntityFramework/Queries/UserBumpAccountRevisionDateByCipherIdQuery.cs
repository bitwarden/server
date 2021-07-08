using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models.EntityFramework;
using Table = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class UserBumpAccountRevisionDateByCipherIdQuery : IQuery<User>
    {
        private readonly Table.Cipher _cipher;

        public UserBumpAccountRevisionDateByCipherIdQuery(Table.Cipher cipher)
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
                    on cc.CollectionId equals collectionUser.CollectionId into cu_g
                from cu in cu_g.DefaultIfEmpty()
                where ou.AccessAll && 
                        cu.OrganizationUserId == ou.Id
                join groupUser in dbContext.GroupUsers
                    on ou.Id equals groupUser.OrganizationUserId into gu_g
                from gu in gu_g.DefaultIfEmpty()
                where cu.CollectionId == null &&
                        !ou.AccessAll
                join grp in dbContext.Groups
                    on gu.GroupId equals grp.Id into g_g
                from g in g_g.DefaultIfEmpty()
                join collectionGroup in dbContext.CollectionGroups
                    on cc.CollectionId equals collectionGroup.CollectionId into cg_g
                from cg in cg_g.DefaultIfEmpty()
                where !g.AccessAll &&
                        cg.GroupId == gu.GroupId
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
}
