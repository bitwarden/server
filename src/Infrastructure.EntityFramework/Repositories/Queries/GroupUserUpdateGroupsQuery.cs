using Bit.Infrastructure.EntityFramework.Models;

namespace Bit.Infrastructure.EntityFramework.Repositories.Queries;

public class GroupUserUpdateGroupsQuery
{
    public readonly GroupUserUpdateGroupsInsertQuery Insert;
    public readonly GroupUserUpdateGroupsDeleteQuery Delete;

    public GroupUserUpdateGroupsQuery(Guid organizationUserId, IEnumerable<Guid> groupIds)
    {
        Insert = new GroupUserUpdateGroupsInsertQuery(organizationUserId, groupIds);
        Delete = new GroupUserUpdateGroupsDeleteQuery(organizationUserId, groupIds);
    }
}

public class GroupUserUpdateGroupsInsertQuery : IQuery<GroupUser>
{
    private readonly Guid _organizationUserId;
    private readonly IEnumerable<Guid> _groupIds;

    public GroupUserUpdateGroupsInsertQuery(Guid organizationUserId, IEnumerable<Guid> collections)
    {
        _organizationUserId = organizationUserId;
        _groupIds = collections;
    }

    public IQueryable<GroupUser> Run(DatabaseContext dbContext)
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
        return query.Select(x => new GroupUser
        {
            GroupId = x.Id,
            OrganizationUserId = _organizationUserId,
        });
    }
}

public class GroupUserUpdateGroupsDeleteQuery : IQuery<GroupUser>
{
    private readonly Guid _organizationUserId;
    private readonly IEnumerable<Guid> _groupIds;

    public GroupUserUpdateGroupsDeleteQuery(Guid organizationUserId, IEnumerable<Guid> groupIds)
    {
        _organizationUserId = organizationUserId;
        _groupIds = groupIds;
    }

    public IQueryable<GroupUser> Run(DatabaseContext dbContext)
    {
        var deleteQuery = from gu in dbContext.GroupUsers
                          where gu.OrganizationUserId == _organizationUserId &&
                              !_groupIds.Any(x => gu.GroupId == x)
                          select gu;
        return deleteQuery;
    }
}
