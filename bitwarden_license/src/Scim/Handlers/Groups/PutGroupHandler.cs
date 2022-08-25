using System;
using System.Net;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Groups;
using Bit.Scim.Context;
using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Handlers.Groups
{
    public class PutGroupHandler : IRequestHandler<PutGroupCommand, RequestResult>
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupService _groupService;
        private readonly IScimContext _scimContext;

        public PutGroupHandler(
            IGroupRepository groupRepository,
            IGroupService groupService,
            IScimContext scimContext)
        {
            _groupRepository = groupRepository;
            _groupService = groupService;
            _scimContext = scimContext;
        }

        public async Task<RequestResult> Handle(PutGroupCommand request, CancellationToken cancellationToken)
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

            group.Name = request.Model.DisplayName;
            await _groupService.SaveAsync(group);
            await UpdateGroupMembersAsync(group, request.Model, false);
            return new RequestResult(true, HttpStatusCode.OK, new ScimGroupResponseModel(group));
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
}
