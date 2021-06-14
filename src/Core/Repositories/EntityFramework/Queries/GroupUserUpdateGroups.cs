using System.Linq;
using EfModel = Bit.Core.Models.EntityFramework;
using Bit.Core.Models.Table;
using Bit.Core.Models.Data;
using System;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Bit.Core.Repositories.EntityFramework.Queries
{
    public class GroupUserUpdateGroups
    {
        public readonly GroupUserUpdateGroupsInsert Insert;
        public readonly GroupUserUpdateGroupsDelete Delete;

        public GroupUserUpdateGroups(Guid organizationUserId, IEnumerable<Guid> groupIds)
        {
            Insert  = new GroupUserUpdateGroupsInsert(organizationUserId, groupIds);
            Delete = new GroupUserUpdateGroupsDelete(organizationUserId, groupIds);
        }
    }

    public class GroupUserUpdateGroupsInsert: IQuery<EfModel.GroupUser>
    {
        private readonly Guid _organizationUserId;
        private readonly IEnumerable<Guid> _groupIds;

        public GroupUserUpdateGroupsInsert(Guid organizationUserId, IEnumerable<Guid> collections)
        {
            _organizationUserId = organizationUserId;
            _groupIds = collections;
        }

        public IQueryable<EfModel.GroupUser> Run(DatabaseContext dbContext)
        {
            var orgUser = from ou in dbContext.OrganizationUsers
                        where ou.Id == _organizationUserId
                        select ou;
            var groupIdEntities = dbContext.Groups.Where(x => _groupIds.Contains(x.Id));
            var query = from g in dbContext.Groups
                        join ou in orgUser
                            on g.OrganizationId equals ou.OrganizationId
                        join gie in groupIdEntities
                            on g.Id equals gie.Id
                        where !dbContext.GroupUsers.Any(gu => _groupIds.Contains(gu.GroupId) && gu.OrganizationUserId == _organizationUserId)
                        select g;
            return query.Select(x => new EfModel.GroupUser() {
                GroupId = x.Id,
                OrganizationUserId = _organizationUserId
            });
        }
    }

    public class GroupUserUpdateGroupsDelete: IQuery<EfModel.GroupUser>
    {
        private readonly Guid _organizationUserId;
        private readonly IEnumerable<Guid> _groupIds;

        public GroupUserUpdateGroupsDelete(Guid organizationUserId, IEnumerable<Guid> groupIds)
        {
            _organizationUserId = organizationUserId;
            _groupIds = groupIds;
        }

        public IQueryable<EfModel.GroupUser> Run(DatabaseContext dbContext)
        {
            var deleteQuery =   from gu in dbContext.GroupUsers
                                where gu.OrganizationUserId == _organizationUserId &&
                                    !_groupIds.Any(x => gu.GroupId == x)
                                select gu;
            return deleteQuery;
        }

    }
}
