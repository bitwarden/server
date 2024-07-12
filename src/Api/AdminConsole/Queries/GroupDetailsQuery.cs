using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;

namespace Api.AdminConsole.Queries;

public class GroupDetailsQueryRequest
{
    public Guid OrganizationId { get; set; }
    public Guid? GroupId { get; set; }
}

public class GroupDetailsQueryResponse
{
    public Group Group { get; set; }
    public IEnumerable<CollectionAccessSelection> CollectionAccessSelection { get; set; }
}

public class GroupDetailsQuery : IGroupDetailsQuery
{
    private readonly IGroupRepository _groupRepository;
    private readonly ICurrentContext _currentContext;

    public GroupDetailsQuery(
        IGroupRepository groupRepository,
        ICurrentContext currentContext)
    {
        _groupRepository = groupRepository;
        _currentContext = currentContext;
    }

    /// <summary>
    /// Query to get group details for an organization
    /// </summary>
    /// <param name="request">Request details for the query. If the GroupId is populated only one group will be returned in the list</param>
    /// <returns>List of GroupDetailsQueryResponse</returns>
    public async Task<IEnumerable<GroupDetailsQueryResponse>> GetGroupDetails(GroupDetailsQueryRequest request)
    {
        if (request.GroupId.HasValue)
        {
            var detail = await GetGroupDetailsById(request.OrganizationId);
            return detail;
        }

        var details = await GetOrganizationGroupDetails(request.OrganizationId);
        return details;
    }

    /// <summary>
    /// Gets the group details for an entire organization
    /// </summary>
    /// <param name="orgId">Id of the organization groups to get</param>
    /// <returns>List of GroupDetailsQueryResponse</returns>
    private async Task<IEnumerable<GroupDetailsQueryResponse>> GetOrganizationGroupDetails(Guid orgId)
    {
        var groupDetails = await _groupRepository.GetManyWithCollectionsByOrganizationIdAsync(orgId);
        var responses = groupDetails.Select(g => new GroupDetailsQueryResponse { Group = g.Item1, CollectionAccessSelection = g.Item2 });
        return responses;
    }

    /// <summary>
    /// Gets an organization group by a group id. A list is returned with only one item. 
    /// </summary>
    /// <param name="groupId">Id of the group to get</param>
    /// <returns>List of GroupDetailsQueryResponse</returns>
    private async Task<IEnumerable<GroupDetailsQueryResponse>> GetGroupDetailsById(Guid groupId)
    {
        var groupDetails = await _groupRepository.GetByIdWithCollectionsAsync(groupId);
        if (groupDetails?.Item1 == null || !await _currentContext.ManageGroups(groupDetails.Item1.OrganizationId))
        {
            throw new NotFoundException();
        }

        var response = new GroupDetailsQueryResponse
        {
            Group = groupDetails.Item1,
            CollectionAccessSelection = groupDetails.Item2
        };

        return new List<GroupDetailsQueryResponse> { response };
    }
}
