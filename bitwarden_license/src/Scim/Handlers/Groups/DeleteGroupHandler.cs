using System.Net;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Groups;
using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Handlers.Groups
{
    public class DeleteGroupHandler : IRequestHandler<DeleteGroupCommand, RequestResult>
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

        public async Task<RequestResult> Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
        {
            var group = await _groupRepository.GetByIdAsync(request.Id);
            if (group == null || group.OrganizationId != request.OrganizationId)
            {
                return new RequestResult(false, HttpStatusCode.NotFound, new ScimErrorResponseModel
                {
                    Status = 404,
                    Detail = "Group not found."
                });
            }
            await _groupService.DeleteAsync(group);

            return new RequestResult();
        }
    }
}
