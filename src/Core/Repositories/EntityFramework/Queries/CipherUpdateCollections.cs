using System.Linq;
using Bit.Core.Models.EntityFramework;
using System;
using Bit.Core.Enums;
using System.Collections.Generic;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class CipherUpdateCollections : IQuery<CollectionCipher>
    {
        private Cipher Cipher { get; set; }
        private IEnumerable<Guid> CollectionIds { get; set; }

        public CipherUpdateCollections(Cipher cipher, IEnumerable<Guid> collectionIds)
        {
            Cipher = cipher;
            CollectionIds = collectionIds;
        }

        public virtual IQueryable<CollectionCipher> Run(DatabaseContext dbContext)
        {
            if (!Cipher.OrganizationId.HasValue || !CollectionIds.Any())
            {
                return null;
            }

            var availibleCollections = !Cipher.UserId.HasValue ?
                from c in dbContext.Collections
                where c.OrganizationId == Cipher.OrganizationId
                select c.Id :
                from c in dbContext.Collections
                join o in dbContext.Organizations
                    on c.OrganizationId equals o.Id
                join ou in dbContext.OrganizationUsers
                    on o.Id equals ou.OrganizationId
                where ou.UserId == Cipher.UserId
                join cu in dbContext.CollectionUsers
                    on c.Id equals cu.CollectionId into cu_g
                from cu in cu_g.DefaultIfEmpty()
                where !ou.AccessAll && cu.OrganizationUserId == ou.Id
                join gu in dbContext.GroupUsers
                    on ou.Id equals gu.OrganizationUserId into gu_g
                from gu in gu_g.DefaultIfEmpty()
                where cu.CollectionId == null && !ou.AccessAll
                join g in dbContext.Groups
                    on gu.GroupId equals g.Id into g_g
                from g in g_g.DefaultIfEmpty()
                join cg in dbContext.CollectionGroups
                    on c.Id equals cg.CollectionId into cg_g
                from cg in cg_g.DefaultIfEmpty()
                where !g.AccessAll && gu.GroupId == cg.GroupId &&
                    o.Id == Cipher.OrganizationId &&
                    o.Enabled &&
                    ou.Status == OrganizationUserStatusType.Confirmed &&
                    (ou.AccessAll || !cu.ReadOnly || g.AccessAll || !cg.ReadOnly)
                select new { c, o, ou, cu, gu, g, cg }.c.Id;

            if (!availibleCollections.Any())
            {
                return null;
            }

            var query = from c in availibleCollections
                        select new CollectionCipher { CollectionId = c, CipherId = Cipher.Id };
            return query;
        }
    }
}
