using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Models.Table;
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

        public async Task<CollectionCipher> CreateAsync(CollectionCipher obj)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var entity = Mapper.Map<EfModel.CollectionCipher>(obj);
                dbContext.Add(entity);
                await dbContext.SaveChangesAsync();
                // TODO: bump account revision date by collectionid
                return obj;
            }
        }

        public Task<ICollection<CollectionCipher>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<CollectionCipher>> GetManyByUserIdAsync(Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<CollectionCipher>> GetManyByUserIdCipherIdAsync(Guid userId, Guid cipherId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateCollectionsAsync(Guid cipherId, Guid userId, IEnumerable<Guid> collectionIds)
        {
            throw new NotImplementedException();
        }

        public Task UpdateCollectionsForAdminAsync(Guid cipherId, Guid organizationId, IEnumerable<Guid> collectionIds)
        {
            throw new NotImplementedException();
        }

        public Task UpdateCollectionsForCiphersAsync(IEnumerable<Guid> cipherIds, Guid userId, Guid organizationId, IEnumerable<Guid> collectionIds)
        {
            throw new NotImplementedException();
        }
    }
}
