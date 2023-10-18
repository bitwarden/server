using Bit.Core.AdminConsole.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.Groups.Interfaces;
using Bit.Core.Repositories;
using Bit.Scim.Context;
using Bit.Scim.Groups.Interfaces;
using Bit.Scim.Models;

namespace Bit.Scim.Groups;

public class PostGroupCommand : IPostGroupCommand
{
    private readonly IGroupRepository _groupRepository;
    private readonly IScimContext _scimContext;
    private readonly ICreateGroupCommand _createGroupCommand;

    public PostGroupCommand(
        IGroupRepository groupRepository,
        IOrganizationRepository organizationRepository,
        IScimContext scimContext,
        ICreateGroupCommand createGroupCommand)
    {
        _groupRepository = groupRepository;
        _scimContext = scimContext;
        _createGroupCommand = createGroupCommand;
    }

    public async Task<Group> PostGroupAsync(Organization organization, ScimGroupRequestModel model)
    {
        if (string.IsNullOrWhiteSpace(model.DisplayName))
        {
            throw new BadRequestException();
        }

        var groups = await _groupRepository.GetManyByOrganizationIdAsync(organization.Id);
        if (!string.IsNullOrWhiteSpace(model.ExternalId) && groups.Any(g => g.ExternalId == model.ExternalId))
        {
            throw new ConflictException();
        }

        var group = model.ToGroup(organization.Id);
        await _createGroupCommand.CreateGroupAsync(group, organization, EventSystemUser.SCIM, collections: null);
        await UpdateGroupMembersAsync(group, model);

        return group;
    }

    private async Task UpdateGroupMembersAsync(Group group, ScimGroupRequestModel model)
    {
        if (_scimContext.RequestScimProvider != ScimProviderType.Okta)
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

        if (!memberIds.Any())
        {
            return;
        }

        await _groupRepository.UpdateUsersAsync(group.Id, memberIds);
    }
}
