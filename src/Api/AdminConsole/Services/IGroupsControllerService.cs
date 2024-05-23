using System.Security.Claims;
using Bit.Api.AdminConsole.Models.Response;

namespace Api.AdminConsole.Services;

public interface IGroupsControllerService
{
    Task<IEnumerable<GroupDetailsResponseModel>> GetGroups(ClaimsPrincipal user, Guid orgId);
}
