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
    public class GroupRepository : Repository<TableModel.Group, EfModel.Group, Guid>, IGroupRepository
    {
        public GroupRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Groups)
        { }

        public Task CreateAsync(Group obj, IEnumerable<SelectionReadOnly> collections)
        {
            throw new NotImplementedException();
        }

        public Task DeleteUserAsync(Guid groupId, Guid organizationUserId)
        {
            throw new NotImplementedException();
        }

        public Task<Tuple<Group, ICollection<SelectionReadOnly>>> GetByIdWithCollectionsAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<Group>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<GroupUser>> GetManyGroupUsersByOrganizationIdAsync(Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<Guid>> GetManyIdsByUserIdAsync(Guid organizationUserId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<Guid>> GetManyUserIdsByIdAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task ReplaceAsync(Group obj, IEnumerable<SelectionReadOnly> collections)
        {
            throw new NotImplementedException();
        }

        public Task UpdateUsersAsync(Guid groupId, IEnumerable<Guid> organizationUserIds)
        {
            throw new NotImplementedException();
        }
    }
}
