using System;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Groups;
using MediatR;

namespace Bit.Scim.Handlers.Groups;

public class DeleteGroupHandler : IRequestHandler<DeleteGroupCommand>
{
    private readonly IEventService _eventService;
    private readonly IGroupRepository _groupRepository;

    public DeleteGroupHandler(
        IEventService eventService,
        IGroupRepository groupRepository)
    {
        _eventService = eventService;
        _groupRepository = groupRepository;
    }

    public async Task<Unit> Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await _groupRepository.GetByIdAsync(request.Id);
        if (group == null || group.OrganizationId != request.OrganizationId)
        {
            throw new NotFoundException("Group not found.");
        }

        await _groupRepository.DeleteAsync(group);
        await _eventService.LogGroupEventAsync(group, Core.Enums.EventType.Group_Deleted);

        return Unit.Value;
    }
}
