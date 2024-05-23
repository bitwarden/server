using System.Security.Claims;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.Enums;
using Bit.Core.Models.Data;


namespace Api.AdminConsole.Services;

public interface IOrganizationUserControllerService
{
    Task<IEnumerable<OrganizationUserUserDetailsResponseModel>> GetOrganizationUserUserDetails(ClaimsPrincipal user, Guid orgId, bool includeGroups = false, bool includeCollections = false);
    Task<IEnumerable<OrganizationUserUserDetailsResponseModel>> Get_vNext(ClaimsPrincipal user, Guid orgId,
        bool includeGroups = false, bool includeCollections = false);
    Task<bool> FlexibleCollectionsIsEnabledAsync(Guid organizationId);
    OrganizationUserType GetFlexibleCollectionsUserType(OrganizationUserType type, Permissions permissions);
}
