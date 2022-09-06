using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Scim.Models;
using Bit.Scim.Queries.Groups;
using MediatR;

namespace Bit.Scim.Handlers.Groups;

public class GetGroupHandler : IRequestHandler<GetGroupQuery, ScimGroupResponseModel>
{
    private readonly IGroupRepository _groupRepository;

    public GetGroupHandler(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<ScimGroupResponseModel> Handle(GetGroupQuery request, CancellationToken cancellationToken)
    {
        var group = await _groupRepository.GetByIdAsync(request.Id);
        if (group == null || group.OrganizationId != request.OrganizationId)
        {
            throw new NotFoundException("Group not found.");
        }

        return new ScimGroupResponseModel(group);
    }
}
