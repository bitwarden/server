using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.Repositories;
using Bit.Scim.Context;
using Bit.Scim.Groups.Interfaces;
using Bit.Scim.Models;

namespace Bit.Scim.Groups;

public class PutGroupCommand : IPutGroupCommand
{
    private readonly IGroupRepository _groupRepository;
    private readonly IScimContext _scimContext;
    private readonly IUpdateGroupCommand _updateGroupCommand;

    public PutGroupCommand(
        IGroupRepository groupRepository,
        IScimContext scimContext,
        IUpdateGroupCommand updateGroupCommand)
    {
        _groupRepository = groupRepository;
        _scimContext = scimContext;
        _updateGroupCommand = updateGroupCommand;
    }

    public async Task<Group> PutGroupAsync(Organization organization, Guid id, ScimGroupRequestModel model)
    {
        _updateGroupCommand.Validate(organization);

        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null || group.OrganizationId != organization.Id)
        {
            throw new NotFoundException("Group not found.");
        }

        group.Name = model.DisplayName;
        await _updateGroupCommand.UpdateGroupAsync(group, EventSystemUser.SCIM);
        await UpdateGroupMembersAsync(group, model);

        return group;
    }

    private async Task UpdateGroupMembersAsync(Group group, ScimGroupRequestModel model)
    {
        if (_scimContext.RequestScimProvider != Core.Enums.ScimProviderType.Okta)
        {
            return;
        }

        if (model.Members == null)
        {
            return;
        }

        var memberIds = new List<Guid>();
        foreach (var id in model.Members.Select(i => i.Value))
        {
            if (Guid.TryParse(id, out var guidId))
            {
                memberIds.Add(guidId);
            }
        }

        await _groupRepository.UpdateUsersAsync(group.Id, memberIds);
    }
}
