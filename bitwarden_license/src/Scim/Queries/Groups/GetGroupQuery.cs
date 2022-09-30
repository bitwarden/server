using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Scim.Models;
using Bit.Scim.Queries.Groups.Interfaces;

namespace Bit.Scim.Queries.Groups;

public class GetGroupQuery : IGetGroupQuery
{
    private readonly IGroupRepository _groupRepository;

    public GetGroupQuery(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<ScimGroupResponseModel> GetGroupAsync(Guid organizationId, Guid groupId)
    {
        var group = await _groupRepository.GetByIdAsync(groupId);
        if (group == null || group.OrganizationId != organizationId)
        {
            throw new NotFoundException("Group not found.");
        }

        return new ScimGroupResponseModel(group);
    }
}
