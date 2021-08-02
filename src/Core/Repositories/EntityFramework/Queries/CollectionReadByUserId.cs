using System.Linq;
using System;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class CollectionReadByUserId : UserCollectionDetailsQuery
    {
        private readonly Guid _userId;

        public CollectionReadByUserId(Guid userId) : base(userId)
        {
            _userId = userId;
        }

        public override IQueryable<CollectionDetails> Run(DatabaseContext dbContext)
        {
            var query = base.Run(dbContext);
            return query
                .GroupBy(c => c.Id)
                .Select(g => new CollectionDetails
                {
                    Id = g.Key,
                    OrganizationId = g.FirstOrDefault().OrganizationId,
                    Name = g.FirstOrDefault().Name,
                    ExternalId = g.FirstOrDefault().ExternalId,
                    CreationDate = g.FirstOrDefault().CreationDate,
                    RevisionDate = g.FirstOrDefault().RevisionDate,
                    ReadOnly = g.Min(c => c.ReadOnly),
                    HidePasswords = g.Min(c => c.HidePasswords)
                });
        }
    }
}
