using System.Security.Claims;
using Bit.Api.Models.Response;

namespace Api.Services;

public interface ICollectionsControllerService
{
    Task<IEnumerable<CollectionResponseModel>> GetOrganizationCollections(ClaimsPrincipal User, Guid orgId);
    Task<bool> FlexibleCollectionsIsEnabledAsync(Guid organizationId);
}
