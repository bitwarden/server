using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class OrganizationUserRepository : Repository<TableModel.OrganizationUser, EfModel.OrganizationUser, Guid>, IOrganizationUserRepository
    {
        public OrganizationUserRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.OrganizationUsers)
        { }

        public Task CreateAsync(OrganizationUser obj, IEnumerable<SelectionReadOnly> collections)
        {
            throw new NotImplementedException();
        }

        public Task<Tuple<OrganizationUser, ICollection<SelectionReadOnly>>> GetByIdWithCollectionsAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<OrganizationUser> GetByOrganizationAsync(Guid organizationId, Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<OrganizationUser> GetByOrganizationEmailAsync(Guid organizationId, string email)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetCountByFreeOrganizationAdminUserAsync(Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetCountByOnlyOwnerAsync(Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetCountByOrganizationAsync(Guid organizationId, string email, bool onlyRegisteredUsers)
        {
            throw new NotImplementedException();
        }

        public Task<int> GetCountByOrganizationIdAsync(Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<OrganizationUserUserDetails> GetDetailsByIdAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<Tuple<OrganizationUserUserDetails, ICollection<SelectionReadOnly>>> GetDetailsByIdWithCollectionsAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<OrganizationUserOrganizationDetails> GetDetailsByUserAsync(Guid userId, Guid organizationId, OrganizationUserStatusType? status = null)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<OrganizationUser>> GetManyByManyUsersAsync(IEnumerable<Guid> userIds)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<OrganizationUser>> GetManyByOrganizationAsync(Guid organizationId, OrganizationUserType? type)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<OrganizationUser>> GetManyByUserAsync(Guid userId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<OrganizationUserUserDetails>> GetManyDetailsByOrganizationAsync(Guid organizationId)
        {
            throw new NotImplementedException();
        }

        public Task<ICollection<OrganizationUserOrganizationDetails>> GetManyDetailsByUserAsync(Guid userId, OrganizationUserStatusType? status = null)
        {
            throw new NotImplementedException();
        }

        public Task ReplaceAsync(OrganizationUser obj, IEnumerable<SelectionReadOnly> collections)
        {
            throw new NotImplementedException();
        }

        public Task UpdateGroupsAsync(Guid orgUserId, IEnumerable<Guid> groupIds)
        {
            throw new NotImplementedException();
        }
    }
}
