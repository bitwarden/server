using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class CollectionRepository : Repository<TableModel.Collection, EfModel.Collection, Guid>, ICollectionRepository
    {
        public CollectionRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Collections)
        { }

        public override async Task<TableModel.Collection> CreateAsync(Collection obj)
        {
            await base.CreateAsync(obj);
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                // TODO: User_BumpAccountRevisionDateByCollectionId
            }
            return obj;
        }

        public Task CreateAsync(Collection obj, IEnumerable<SelectionReadOnly> groups)
        {
            throw new NotImplementedException();
        }

        public Task DeleteUserAsync(Guid collectionId, Guid organizationUserId)
        {
            throw new NotImplementedException();
        }

        public Task<CollectionDetails> GetByIdAsync(Guid id, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<Tuple<Collection, ICollection<SelectionReadOnly>>> GetByIdWithGroupsAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<Tuple<CollectionDetails, ICollection<SelectionReadOnly>>> GetByIdWithGroupsAsync(Guid id, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetCountByOrganizationIdAsync(Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<Collection>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<CollectionDetails>> GetManyByUserIdAsync(Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<SelectionReadOnly>> GetManyUsersByIdAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task ReplaceAsync(Collection obj, IEnumerable<SelectionReadOnly> groups)
        {
            throw new NotImplementedException();
        }

        public Task UpdateUsersAsync(Guid id, IEnumerable<SelectionReadOnly> users)
        {
            throw new NotImplementedException();
        }
    }
}
