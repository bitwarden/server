using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Groups;
using Bit.Scim.Context;
using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Handlers.Groups;

public class PostGroupHandler : IRequestHandler<PostGroupCommand, Group>
{
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupService _groupService;
    private readonly IScimContext _scimContext;

    public PostGroupHandler(
        IGroupRepository groupRepository,
        IGroupService groupService,
        IScimContext scimContext)
    {
        _groupRepository = groupRepository;
        _groupService = groupService;
        _scimContext = scimContext;
    }

    public async Task<Group> Handle(PostGroupCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Model.DisplayName))
        {
            throw new BadRequestException();
        }

        var groups = await _groupRepository.GetManyByOrganizationIdAsync(request.OrganizationId);
        if (!string.IsNullOrWhiteSpace(request.Model.ExternalId) && groups.Any(g => g.ExternalId == request.Model.ExternalId))
        {
            throw new ConflictException();
        }

        var group = request.Model.ToGroup(request.OrganizationId);
        await _groupService.SaveAsync(group, null);
        await UpdateGroupMembersAsync(group, request.Model, true);

        return group;
    }

    private async Task UpdateGroupMembersAsync(Group group, ScimGroupRequestModel model, bool skipIfEmpty)
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

        if (!memberIds.Any() && skipIfEmpty)
        {
            return;
        }

        await _groupRepository.UpdateUsersAsync(group.Id, memberIds);
    }
}
