using System.Linq;
using Bit.Core.Models.EntityFramework;
using System;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class CollectionCipherReadByUserIdCipherIdQuery : CollectionCipherReadByUserIdQuery
    {
        private readonly Guid _cipherId;

        public CollectionCipherReadByUserIdCipherIdQuery(Guid userId, Guid cipherId) : base(userId)
        {
            _cipherId = cipherId;
        }

        public override IQueryable<CollectionCipher> Run(DatabaseContext dbContext)
        {
            var query = base.Run(dbContext);
            return query.Where(x => x.CipherId == _cipherId);
        }
    }
}
