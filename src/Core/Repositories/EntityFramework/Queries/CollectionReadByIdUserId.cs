using System.Linq;
using Bit.Core.Models.EntityFramework;
using System;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class CollectionReadByIdUserId : CollectionReadByUserId
    {
        private readonly Guid _id;

        public CollectionReadByIdUserId(Guid id, Guid userId) : base(userId)
        {
            _id = id;
        }

        public override IQueryable<CollectionDetails> Run(DatabaseContext dbContext)
        {
            var query = base.Run(dbContext);
            return query.Where(c => c.Id == _id);
        }
    }
}
