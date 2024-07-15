using Api.AdminConsole.Services;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.AdminConsole.Models.Response;
using Bit.Api.Models.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/{orgId}/groups")]
[Authorize("Application")]
public class GroupsController : Controller
{
    private readonly IGroupsControllerService _groupsControllerService;

    public GroupsController(
        IGroupsControllerService groupsControllerService)
    {
        _groupsControllerService = groupsControllerService;
    }

    [HttpGet("{id}")]
    public async Task<GroupResponseModel> Get(string orgId, string id)
    {
        var group = await _groupsControllerService.GetOrganizationGroup(orgId, id);
        return group;
    }

    [HttpGet("{id}/details")]
    public async Task<GroupDetailsResponseModel> GetDetails(string orgId, string id)
    {
        var groupDetails = await _groupsControllerService.GetOrganizationGroupDetail(orgId, id);
        return groupDetails;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<GroupDetailsResponseModel>> Get(Guid orgId)
    {
        var responses = await _groupsControllerService.GetOrganizationGroupsDetails(User, orgId);
        return new ListResponseModel<GroupDetailsResponseModel>(responses);
    }

    [HttpGet("{id}/users")]
    public async Task<IEnumerable<Guid>> GetUsers(string orgId, string id)
    {
        var userIds = await _groupsControllerService.GetOrganizationUsers(orgId);
        return userIds;
    }

    [HttpPost("")]
    public async Task<GroupResponseModel> Post(Guid orgId, [FromBody] GroupRequestModel model)
    {
        var group = await _groupsControllerService.CreateGroup(User, orgId, model);
        return group;
    }

    [HttpPut("{id}")]
    [HttpPost("{id}")]
    public async Task<GroupResponseModel> Put(Guid orgId, Guid id, [FromBody] GroupRequestModel model)
    {
        var group = await _groupsControllerService.UpdateGroup(User, orgId, id, model);
        return group;
    }

    [HttpDelete("{id}")]
    [HttpPost("{id}/delete")]
    public async Task Delete(string orgId, string id)
    {
        await _groupsControllerService.DeleteGroup(orgId, id);
    }

    [HttpDelete("")]
    [HttpPost("delete")]
    public async Task BulkDelete([FromBody] GroupBulkRequestModel model)
    {
        await _groupsControllerService.BulkDeleteGroups(model);
    }

    [HttpDelete("{id}/user/{orgUserId}")]
    [HttpPost("{id}/delete-user/{orgUserId}")]
    public async Task Delete(string orgId, string id, string orgUserId)
    {
        await _groupsControllerService.DeleteGroupUser(orgId, id, orgUserId);
    }
}
