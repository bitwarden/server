// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Scim.Models;
using Bit.Scim.Users.Interfaces;
using Bit.Scim.Utilities;

namespace Bit.Scim.Users;

public class GetUsersListQuery : IGetUsersListQuery
{
    private readonly IOrganizationUserRepository _organizationUserRepository;

    public GetUsersListQuery(IOrganizationUserRepository organizationUserRepository)
    {
        _organizationUserRepository = organizationUserRepository;
    }

    public async Task<(IEnumerable<OrganizationUserUserDetails> userList, int totalResults)> GetUsersListAsync(Guid organizationId, GetUsersQueryParamModel userQueryParams)
    {
        int count = userQueryParams.Count;
        int startIndex = userQueryParams.StartIndex;
        string filter = userQueryParams.Filter;

        var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
        var userList = new List<OrganizationUserUserDetails>();
        var totalResults = 0;

        var predicate = ScimFilterParser.TryGetPredicate<OrganizationUserUserDetails>(filter,
            new Dictionary<string, Func<OrganizationUserUserDetails, string>>
            {
                ["username"] = ou => ou.Email,
                ["externalid"] = ou => ou.ExternalId
            });

        IEnumerable<OrganizationUserUserDetails> filtered;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            filtered = predicate != null
                ? orgUsers.Where(predicate)
                : Enumerable.Empty<OrganizationUserUserDetails>();
        }
        else
        {
            filtered = orgUsers.AsEnumerable();
        }

        totalResults = filtered.Count();
        userList = filtered.OrderBy(ou => ou.Email)
            .Skip(startIndex - 1)
            .Take(count)
            .ToList();

        return (userList, totalResults);
    }
}
