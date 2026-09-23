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

        if (!string.IsNullOrWhiteSpace(filter))
        {
            if (ScimFilterParser.Parse(filter, out var attribute, out var op, out var value))
            {
                Func<OrganizationUserUserDetails, string> selector = attribute switch
                {
                    "username" => ou => ou.Email,
                    "externalid" => ou => ou.ExternalId,
                    _ => null
                };

                if (selector != null)
                {
                    userList = orgUsers
                        .Where(ou => ScimFilterParser.Matches(selector(ou), op, value))
                        .ToList();
                    totalResults = userList.Count;
                }
            }
        }
        else
        {
            userList = orgUsers.OrderBy(ou => ou.Email)
                .Skip(startIndex - 1)
                .Take(count)
                .ToList();
            totalResults = orgUsers.Count;
        }

        return (userList, totalResults);
    }
}
