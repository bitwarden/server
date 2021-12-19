﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.Repositories
{
    public interface IOrganizationUserRepository : IRepository<OrganizationUser, Guid>
    {
        Task<int> GetCountByOrganizationIdAsync(Guid organizationId);
        Task<int> GetCountByFreeOrganizationAdminUserAsync(Guid userId);
        Task<int> GetCountByOnlyOwnerAsync(Guid userId);
        Task<ICollection<OrganizationUser>> GetManyByUserAsync(Guid userId);
        Task<ICollection<OrganizationUser>> GetManyByOrganizationAsync(Guid organizationId, OrganizationUserType? type);
        Task<int> GetCountByOrganizationAsync(Guid organizationId, string email, bool onlyRegisteredUsers);
        Task<ICollection<string>> SelectKnownEmailsAsync(Guid organizationId, IEnumerable<string> emails, bool onlyRegisteredUsers);
        Task<OrganizationUser> GetByOrganizationAsync(Guid organizationId, Guid userId);
        Task<Tuple<OrganizationUser, ICollection<SelectionReadOnly>>> GetByIdWithCollectionsAsync(Guid id);
        Task<OrganizationUserUserDetails> GetDetailsByIdAsync(Guid id);
        Task<Tuple<OrganizationUserUserDetails, ICollection<SelectionReadOnly>>>
            GetDetailsByIdWithCollectionsAsync(Guid id);
        Task<ICollection<OrganizationUserUserDetails>> GetManyDetailsByOrganizationAsync(Guid organizationId);
        Task<ICollection<OrganizationUserOrganizationDetails>> GetManyDetailsByUserAsync(Guid userId,
            OrganizationUserStatusType? status = null);
        Task<OrganizationUserOrganizationDetails> GetDetailsByUserAsync(Guid userId, Guid organizationId,
            OrganizationUserStatusType? status = null);
        Task UpdateGroupsAsync(Guid orgUserId, IEnumerable<Guid> groupIds);
        Task UpsertManyAsync(IEnumerable<OrganizationUser> organizationUsers);
        Task<Guid> CreateAsync(OrganizationUser obj, IEnumerable<SelectionReadOnly> collections);
        Task<ICollection<Guid>> CreateManyAsync(IEnumerable<OrganizationUser> organizationIdUsers);
        Task ReplaceAsync(OrganizationUser obj, IEnumerable<SelectionReadOnly> collections);
        Task ReplaceManyAsync(IEnumerable<OrganizationUser> organizationUsers);
        Task<ICollection<OrganizationUser>> GetManyByManyUsersAsync(IEnumerable<Guid> userIds);
        Task<ICollection<OrganizationUser>> GetManyAsync(IEnumerable<Guid> Ids);
        Task DeleteManyAsync(IEnumerable<Guid> userIds);
        Task<OrganizationUser> GetByOrganizationEmailAsync(Guid organizationId, string email);
        Task<IEnumerable<OrganizationUserPublicKey>> GetManyPublicKeysByOrganizationUserAsync(Guid organizationId, IEnumerable<Guid> Ids);
        Task<IEnumerable<OrganizationUserUserDetails>> GetManyByMinimumRoleAsync(Guid organizationId, OrganizationUserType minRole);
    }
}
