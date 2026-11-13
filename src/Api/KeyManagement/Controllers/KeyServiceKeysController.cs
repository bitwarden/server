using Bit.Api.KeyManagement.Models.Responses;
using Bit.Core.KeyManagement.KeyService;
using Bit.Core.Models.Api;
using Bit.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.KeyManagement.Controllers;

/// <summary>
/// The Key Service's key reads, served from the existing key columns.
/// </summary>
[Route("v1/keys")]
[Authorize("Application")]
public class KeyServiceKeysController(
    IUserService userService,
    IPublicKeysQuery publicKeysQuery,
    IOwnKeysQuery ownKeysQuery) : Controller
{
    /// <summary>Bounds the owners one request reads, since each is read on its own.</summary>
    private const int MaxOwners = 100;

    /// <summary>
    /// Lists the public keys of the named owners: one of <c>filter[user]</c>, <c>filter[organization]</c> or
    /// <c>filter[provider]</c>, narrowed by <c>filter[keyPurpose]</c> and <c>filter[keyId]</c>.
    /// </summary>
    [HttpGet("public")]
    public async Task<ActionResult<KeyServiceDocument<IEnumerable<KeyServiceResource<PublicKeyAttributes>>>>>
        GetPublicKeysAsync(
            [FromQuery(Name = "filter[user]")] string? user,
            [FromQuery(Name = "filter[organization]")] string? organization,
            [FromQuery(Name = "filter[provider]")] string? provider,
            [FromQuery(Name = "filter[keyPurpose]")] string? keyPurpose,
            [FromQuery(Name = "filter[keyId]")] string? keyId)
    {
        var ownerFilters = new (KeyOwnerType Type, string? Values)[]
            {
                (KeyOwnerType.User, user),
                (KeyOwnerType.Organization, organization),
                (KeyOwnerType.Provider, provider),
            }
            .Where(filter => filter.Values is not null)
            .ToList();

        // Nobody reads every account's keys, so a list always names its owners, all of one type.
        if (ownerFilters.Count == 0)
        {
            return UnprocessableEntity(new ErrorResponseModel(
                "Name whose keys to read: filter[user], filter[organization] or filter[provider]."));
        }

        if (ownerFilters.Count > 1)
        {
            return UnprocessableEntity(new ErrorResponseModel("Name owners of one type only."));
        }

        var (ownerType, values) = ownerFilters[0];
        var ownerIds = KeyServiceFilters.Ids(KeyServiceFilters.Values(values)!);
        if (ownerIds is null || ownerIds.Count == 0)
        {
            return UnprocessableEntity(new ErrorResponseModel("Owners must be ids."));
        }

        if (ownerIds.Count > MaxOwners)
        {
            return UnprocessableEntity(new ErrorResponseModel($"Name at most {MaxOwners} owners."));
        }

        var filter = new KeyFilter(KeyServiceFilters.Values(keyPurpose), KeyServiceFilters.Values(keyId));
        var keys = await publicKeysQuery.Run(ownerType, ownerIds, filter);

        return new KeyServiceDocument<IEnumerable<KeyServiceResource<PublicKeyAttributes>>>(
            keys.Select(PublicKeyAttributes.Resource));
    }

    /// <summary>
    /// Lists the current user's own keys, narrowed by <c>filter[keyPurpose]</c> and <c>filter[keyId]</c>.
    /// </summary>
    [HttpGet("own")]
    public async Task<KeyServiceDocument<IEnumerable<KeyServiceResource<OwnKeyAttributes>>>> GetOwnKeysAsync(
        [FromQuery(Name = "filter[keyPurpose]")] string? keyPurpose,
        [FromQuery(Name = "filter[keyId]")] string? keyId)
    {
        var user = await userService.GetUserByPrincipalAsync(User) ?? throw new UnauthorizedAccessException();

        var filter = new KeyFilter(KeyServiceFilters.Values(keyPurpose), KeyServiceFilters.Values(keyId));
        var keys = await ownKeysQuery.Run(user, filter);

        return new(keys.Select(OwnKeyAttributes.Resource));
    }
}
