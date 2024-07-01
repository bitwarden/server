using System.Security.Claims;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Api.AdminConsole.Models.Response;

namespace Api.AdminConsole.Services;

public interface IGroupsControllerService
{
    Task<GroupResponseModel> GetOrganizationGroup(string orgId, string groupId);
    Task<GroupDetailsResponseModel> GetOrganizationGroupDetail(string orgId, string groupId);
    Task<IEnumerable<GroupDetailsResponseModel>> GetOrganizationGroupsDetails(ClaimsPrincipal user, Guid orgId);
    Task<IEnumerable<Guid>> GetOrganizationUsers(string orgId);
    Task<GroupResponseModel> CreateGroup(ClaimsPrincipal user, Guid orgId, GroupRequestModel model);
    Task<GroupResponseModel> UpdateGroup(ClaimsPrincipal user, Guid orgId, Guid groupId, GroupRequestModel model);
    Task DeleteGroup(string orgId, string groupId);
    Task BulkDeleteGroups(GroupBulkRequestModel model);
    Task DeleteGroupUser(string orgId, string groupId, string orgUserId);
}
