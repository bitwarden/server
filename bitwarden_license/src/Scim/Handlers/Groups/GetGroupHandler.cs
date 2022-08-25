using System.Net;
using Bit.Core.Repositories;
using Bit.Scim.Models;
using Bit.Scim.Queries.Groups;
using MediatR;

namespace Bit.Scim.Handlers.Groups
{
    public class GetGroupHandler : IRequestHandler<GetGroupQuery, RequestResult>
    {
        private readonly IGroupRepository _groupRepository;

        public GetGroupHandler(IGroupRepository groupRepository)
        {
            _groupRepository = groupRepository;
        }

        public async Task<RequestResult> Handle(GetGroupQuery request, CancellationToken cancellationToken)
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
            return new RequestResult(true, HttpStatusCode.OK, new ScimGroupResponseModel(group));
        }
    }
}
