using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Repositories.EntityFramework.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class CollectionCipherRepository : BaseEntityFrameworkRepository, ICollectionCipherRepository
    {
        public CollectionCipherRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper)
        { }

        public async Task<ICollection<CollectionCipher>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var data = await  (
                    from cc in dbContext.CollectionCiphers
                    join c in dbContext.Collections 
                        on cc.CollectionId equals c.Id
                    where c.OrganizationId == organizationId
                    select new { cc, c }).ToListAsync();
                return data.Select(x => x.cc).ToArray();
            }
        }

        public async Task<ICollection<CollectionCipher>> GetManyByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var data = await new CollectionCipherReadByUserId(userId).Run(dbContext).ToListAsync();
                return data;
            }
        }

        public async Task<ICollection<CollectionCipher>> GetManyByUserIdCipherIdAsync(Guid userId, Guid cipherId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var data = await new CollectionCipherReadByUserIdCipherId(userId, cipherId).Run(dbContext).ToListAsync();
                return data;
            }
        }

        public async Task UpdateCollectionsAsync(Guid cipherId, Guid userId, IEnumerable<Guid> collectionIds)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                // TODO: CollectionCipher_UpdateCollections
                // TODO: User_BumpAccountRevisionDateByOrganizationId
            }
            throw new NotImplementedException();
        }

        public async Task UpdateCollectionsForAdminAsync(Guid cipherId, Guid organizationId, IEnumerable<Guid> collectionIds)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                // TODO: CollectionCipher_UpdateCollectionsAdmin
                // TODO: User_BumpAccountRevisionDateByOrganizationId
            }
            throw new NotImplementedException();
        }

        public async Task UpdateCollectionsForCiphersAsync(IEnumerable<Guid> cipherIds, Guid userId, Guid organizationId, IEnumerable<Guid> collectionIds)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                // TODO: CollectionCipher_UpdateCollectionsForCiphers
                // TODO: User_BumpAccountRevisionDateByOrganizationId
            }
            throw new NotImplementedException();
        }
    }
}
