using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Scim.Commands.Groups.Interfaces;
using Bit.Scim.Models;

namespace Bit.Scim.Commands.Groups;

public class GetGroupCommand : IGetGroupCommand
{
    private readonly IGroupRepository _groupRepository;

    public GetGroupCommand(IGroupRepository groupRepository)
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
