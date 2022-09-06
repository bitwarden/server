using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Groups;
using MediatR;

namespace Bit.Scim.Handlers.Groups;

public class DeleteGroupHandler : IRequestHandler<DeleteGroupCommand>
{
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupService _groupService;

    public DeleteGroupHandler(
        IGroupRepository groupRepository,
        IGroupService groupService)
    {
        _groupRepository = groupRepository;
        _groupService = groupService;
    }

    public async Task<Unit> Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await _groupRepository.GetByIdAsync(request.Id);
        if (group == null || group.OrganizationId != request.OrganizationId)
        {
            throw new NotFoundException("Group not found.");
        }

        await _groupService.DeleteAsync(group);

        return Unit.Value;
    }
}
