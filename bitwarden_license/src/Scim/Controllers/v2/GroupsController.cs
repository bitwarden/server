using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Scim.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bit.Scim.Controllers.v2
{
    [Authorize("Scim")]
    [Route("v2/{organizationId}/groups")]
    public class GroupsController : Controller
    {
        private readonly ScimSettings _scimSettings;
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupService _groupService;
        private readonly ILogger<GroupsController> _logger;

        public GroupsController(
            IGroupRepository groupRepository,
            IGroupService groupService,
            IOptions<ScimSettings> scimSettings,
            ILogger<GroupsController> logger)
        {
            _scimSettings = scimSettings?.Value;
            _groupRepository = groupRepository;
            _groupService = groupService;
            _logger = logger;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid organizationId, Guid id)
        {
            var group = await _groupRepository.GetByIdAsync(id);
            if (group == null || group.OrganizationId != organizationId)
            {
                return new NotFoundObjectResult(new ScimErrorResponseModel
                {
                    Status = 404,
                    Detail = "Group not found."
                });
            }
            return new ObjectResult(new ScimGroupResponseModel(group));
        }

        [HttpGet("")]
        public async Task<IActionResult> Get(
            Guid organizationId,
            [FromQuery] int? count,
            [FromQuery] int? startIndex)
        {
            var groups = await _groupRepository.GetManyByOrganizationIdAsync(organizationId);
            var groupList = groups.OrderBy(g => g.Name)
                .Skip(startIndex.Value - 1) // Should this be offset by 1 or not?
                .Take(count.Value)
                .Select(g => new ScimGroupResponseModel(g))
                .ToList();

            var result = new ScimListResponseModel<ScimGroupResponseModel>
            {
                Resources = groupList,
                ItemsPerPage = count.GetValueOrDefault(groupList.Count),
                TotalResults = groups.Count,
                StartIndex = startIndex.GetValueOrDefault(1),
            };
            return new ObjectResult(result);
        }

        [HttpPost("")]
        public async Task<IActionResult> Post(Guid organizationId, [FromBody] ScimGroupRequestModel model)
        {
            if (string.IsNullOrWhiteSpace(model.DisplayName))
            {
                return new BadRequestResult();
            }

            var groups = await _groupRepository.GetManyByOrganizationIdAsync(organizationId);
            if (!string.IsNullOrWhiteSpace(model.ExternalId) && groups.Any(g => g.ExternalId == model.ExternalId))
            {
                return new ConflictResult();
            }

            var group = model.ToGroup(organizationId);
            await _groupService.SaveAsync(group, null);
            var response = new ScimGroupResponseModel(group);
            // TODO: Absolute URL generation using global settings service URLs for SCIM service
            return new CreatedResult(Url.Action(nameof(Get), new { group.OrganizationId, group.Id }), response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(Guid organizationId, Guid id, [FromBody] ScimGroupRequestModel model)
        {
            var group = await _groupRepository.GetByIdAsync(id);
            if (group == null || group.OrganizationId != organizationId)
            {
                return new NotFoundObjectResult(new ScimErrorResponseModel
                {
                    Status = 404,
                    Detail = "Group not found."
                });
            }
            group.Name = model.DisplayName;
            await _groupService.SaveAsync(group);
            return new ObjectResult(new ScimGroupResponseModel(group));
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> Patch(Guid organizationId, Guid id, [FromBody] ScimPatchModel model)
        {
            var group = await _groupRepository.GetByIdAsync(id);
            if (group == null || group.OrganizationId != organizationId)
            {
                return new NotFoundObjectResult(new ScimErrorResponseModel
                {
                    Status = 404,
                    Detail = "Group not found."
                });
            }
            var replaceOp = model.Operations?.FirstOrDefault(o => o.Op == "replace");
            if (replaceOp != null)
            {
                if(replaceOp.Path == "members")
                {
                    var ids = GetValueIds(replaceOp.Value);
                    await _groupRepository.UpdateUsersAsync(group.Id, ids);
                }
                else if (replaceOp.Value.TryGetProperty("displayName", out var displayNameProperty))
                {
                    group.Name = displayNameProperty.GetString();
                }
            }
            var addMembersOp = model.Operations?.FirstOrDefault(o => o.Op == "add" && o.Path == "members");
            if (addMembersOp != null)
            {
                var orgUserIds = (await _groupRepository.GetManyUserIdsByIdAsync(group.Id)).ToHashSet();
                foreach (var v in GetValueIds(replaceOp.Value))
                {
                    orgUserIds.Add(v);
                }
                await _groupRepository.UpdateUsersAsync(group.Id, orgUserIds);
            }
            var removeMembersOp = model.Operations?.FirstOrDefault(
                o => o.Op == "remove" && !string.IsNullOrWhiteSpace(o.Path) && o.Path.StartsWith("members[value eq "));
            if (removeMembersOp != null)
            {
                var removeId = removeMembersOp.Path.Substring(19).Replace("\\\"]", string.Empty);
                if (Guid.TryParse(removeId, out var orgUserId))
                {
                    await _groupService.DeleteUserAsync(group, orgUserId);
                }
            }
            await _groupService.SaveAsync(group);
            return new NoContentResult();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid organizationId, Guid id)
        {
            var group = await _groupRepository.GetByIdAsync(id);
            if (group == null || group.OrganizationId != organizationId)
            {
                return new NotFoundObjectResult(new ScimErrorResponseModel
                {
                    Status = 404,
                    Detail = "Group not found."
                });
            }
            await _groupService.DeleteAsync(group);
            return new NoContentResult();
        }

        private List<Guid> GetValueIds(JsonElement objArray)
        {
            var ids = new List<Guid>();
            foreach(var obj in objArray.EnumerateArray())
            {
                if(obj.TryGetProperty("value", out var valueProperty))
                {
                    if(valueProperty.TryGetGuid(out var guid))
                    {
                        ids.Add(guid);
                    }
                }
            }
            return ids;
        }
    }
}
