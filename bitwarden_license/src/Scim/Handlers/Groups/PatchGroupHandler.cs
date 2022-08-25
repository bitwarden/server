using System.Net;
using System.Text.Json;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Commands.Groups;
using Bit.Scim.Models;
using MediatR;

namespace Bit.Scim.Handlers.Groups
{
    public class PatchGroupHandler : IRequestHandler<PatchGroupCommand, RequestResult>
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupService _groupService;
        private readonly ILogger<PatchGroupHandler> _logger;

        public PatchGroupHandler(
            IGroupRepository groupRepository,
            IGroupService groupService,
            ILogger<PatchGroupHandler> logger)
        {
            _groupRepository = groupRepository;
            _groupService = groupService;
            _logger = logger;
        }

        public async Task<RequestResult> Handle(PatchGroupCommand request, CancellationToken cancellationToken)
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

            var operationHandled = false;
            foreach (var operation in request.Model.Operations)
            {
                // Replace operations
                if (operation.Op?.ToLowerInvariant() == "replace")
                {
                    // Replace a list of members
                    if (operation.Path?.ToLowerInvariant() == "members")
                    {
                        var ids = GetOperationValueIds(operation.Value);
                        await _groupRepository.UpdateUsersAsync(group.Id, ids);
                        operationHandled = true;
                    }
                    // Replace group name from path
                    else if (operation.Path?.ToLowerInvariant() == "displayname")
                    {
                        group.Name = operation.Value.GetString();
                        await _groupService.SaveAsync(group);
                        operationHandled = true;
                    }
                    // Replace group name from value object
                    else if (string.IsNullOrWhiteSpace(operation.Path) &&
                        operation.Value.TryGetProperty("displayName", out var displayNameProperty))
                    {
                        group.Name = displayNameProperty.GetString();
                        await _groupService.SaveAsync(group);
                        operationHandled = true;
                    }
                }
                // Add a single member
                else if (operation.Op?.ToLowerInvariant() == "add" &&
                    !string.IsNullOrWhiteSpace(operation.Path) &&
                    operation.Path.ToLowerInvariant().StartsWith("members[value eq "))
                {
                    var addId = GetOperationPathId(operation.Path);
                    if (addId.HasValue)
                    {
                        var orgUserIds = (await _groupRepository.GetManyUserIdsByIdAsync(group.Id)).ToHashSet();
                        orgUserIds.Add(addId.Value);
                        await _groupRepository.UpdateUsersAsync(group.Id, orgUserIds);
                        operationHandled = true;
                    }
                }
                // Add a list of members
                else if (operation.Op?.ToLowerInvariant() == "add" &&
                    operation.Path?.ToLowerInvariant() == "members")
                {
                    var orgUserIds = (await _groupRepository.GetManyUserIdsByIdAsync(group.Id)).ToHashSet();
                    foreach (var v in GetOperationValueIds(operation.Value))
                    {
                        orgUserIds.Add(v);
                    }
                    await _groupRepository.UpdateUsersAsync(group.Id, orgUserIds);
                    operationHandled = true;
                }
                // Remove a single member
                else if (operation.Op?.ToLowerInvariant() == "remove" &&
                    !string.IsNullOrWhiteSpace(operation.Path) &&
                    operation.Path.ToLowerInvariant().StartsWith("members[value eq "))
                {
                    var removeId = GetOperationPathId(operation.Path);
                    if (removeId.HasValue)
                    {
                        await _groupService.DeleteUserAsync(group, removeId.Value);
                        operationHandled = true;
                    }
                }
                // Remove a list of members
                else if (operation.Op?.ToLowerInvariant() == "remove" &&
                    operation.Path?.ToLowerInvariant() == "members")
                {
                    var orgUserIds = (await _groupRepository.GetManyUserIdsByIdAsync(group.Id)).ToHashSet();
                    foreach (var v in GetOperationValueIds(operation.Value))
                    {
                        orgUserIds.Remove(v);
                    }
                    await _groupRepository.UpdateUsersAsync(group.Id, orgUserIds);
                    operationHandled = true;
                }
            }

            if (!operationHandled)
            {
                _logger.LogWarning("Group patch operation not handled: {0} : ",
                    string.Join(", ", request.Model.Operations.Select(o => $"{o.Op}:{o.Path}")));
            }

            return new RequestResult();
        }

        private List<Guid> GetOperationValueIds(JsonElement objArray)
        {
            var ids = new List<Guid>();
            foreach (var obj in objArray.EnumerateArray())
            {
                if (obj.TryGetProperty("value", out var valueProperty))
                {
                    if (valueProperty.TryGetGuid(out var guid))
                    {
                        ids.Add(guid);
                    }
                }
            }
            return ids;
        }

        private Guid? GetOperationPathId(string path)
        {
            // Parse Guid from string like: members[value eq "{GUID}"}]
            if (Guid.TryParse(path.Substring(18).Replace("\"]", string.Empty), out var id))
            {
                return id;
            }
            return null;
        }
    }
}

