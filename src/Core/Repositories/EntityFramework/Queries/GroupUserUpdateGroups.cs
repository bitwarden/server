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
        public GroupUserUpdateGroupsInsert Insert { get; set; }
        public GroupUserUpdateGroupsDelete Delete { get; set; }

        public GroupUserUpdateGroups(Guid organizationUserId, IEnumerable<Guid> groupIds)
        {
            Insert  = new GroupUserUpdateGroupsInsert(organizationUserId, groupIds);
            Delete = new GroupUserUpdateGroupsDelete(organizationUserId, groupIds);
        }
    }

    public class GroupUserUpdateGroupsInsert: IQuery<EfModel.GroupUser>
    {
        private Guid OrganizationUserId { get; set; }
        private IEnumerable<Guid> GroupIds { get; set; }

        public GroupUserUpdateGroupsInsert(Guid organizationUserId, IEnumerable<Guid> collections)
        {
            OrganizationUserId = organizationUserId;
            GroupIds = collections;
        }

        public IQueryable<EfModel.GroupUser> Run(DatabaseContext dbContext)
        {
            var orgUser = from ou in dbContext.OrganizationUsers
                        where ou.Id == OrganizationUserId
                        select ou;
            var groupIdEntities = dbContext.Groups.Where(x => GroupIds.Contains(x.Id));
            var query = from g in dbContext.Groups
                        join ou in orgUser
                            on g.OrganizationId equals ou.OrganizationId
                        join gie in groupIdEntities
                            on g.Id equals gie.Id
                        where !dbContext.GroupUsers.Any(gu => GroupIds.Contains(gu.GroupId) && gu.OrganizationUserId == OrganizationUserId)
                        select g;
            return query.Select(x => new EfModel.GroupUser() {
                GroupId = x.Id,
                OrganizationUserId = OrganizationUserId
            });
        }
    }

    public class GroupUserUpdateGroupsDelete: IQuery<EfModel.GroupUser>
    {
        private Guid OrganizationUserId  { get; set; }
        private IEnumerable<Guid> GroupIds { get; set; }

        public GroupUserUpdateGroupsDelete(Guid organizationUserId, IEnumerable<Guid> collections)
        {
            OrganizationUserId = organizationUserId;
            GroupIds = collections;
        }

        public IQueryable<EfModel.GroupUser> Run(DatabaseContext dbContext)
        {
            var deleteQuery =   from gu in dbContext.GroupUsers
                                where gu.OrganizationUserId == OrganizationUserId &&
                                    !GroupIds.Any(x => gu.GroupId == x)
                                select gu;
            return deleteQuery;
        }

    }
}
