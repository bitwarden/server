using Bit.Core.Repositories;
using Bit.Scim.Commands.Users.Interfaces;
using Bit.Scim.Models;

namespace Bit.Scim.Commands.Users
{
    public class GetUsersListCommand : IGetUsersListCommand
    {
        private readonly IOrganizationUserRepository _organizationUserRepository;

        public GetUsersListCommand(IOrganizationUserRepository organizationUserRepository)
        {
            _organizationUserRepository = organizationUserRepository;
        }

        public async Task<ScimListResponseModel<ScimUserResponseModel>> GetUsersListAsync(Guid organizationId, string filter, int? count, int? startIndex)
        {
            string emailFilter = null;
            string usernameFilter = null;
            string externalIdFilter = null;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                if (filter.StartsWith("userName eq "))
                {
                    usernameFilter = filter.Substring(12).Trim('"').ToLowerInvariant();
                    if (usernameFilter.Contains("@"))
                    {
                        emailFilter = usernameFilter;
                    }
                }
                else if (filter.StartsWith("externalId eq "))
                {
                    externalIdFilter = filter.Substring(14).Trim('"');
                }
            }

            var userList = new List<ScimUserResponseModel> { };
            var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
            var totalResults = 0;
            if (!string.IsNullOrWhiteSpace(emailFilter))
            {
                var orgUser = orgUsers.FirstOrDefault(ou => ou.Email.ToLowerInvariant() == emailFilter);
                if (orgUser != null)
                {
                    userList.Add(new ScimUserResponseModel(orgUser));
                }
                totalResults = userList.Count;
            }
            else if (!string.IsNullOrWhiteSpace(externalIdFilter))
            {
                var orgUser = orgUsers.FirstOrDefault(ou => ou.ExternalId == externalIdFilter);
                if (orgUser != null)
                {
                    userList.Add(new ScimUserResponseModel(orgUser));
                }
                totalResults = userList.Count;
            }
            else if (string.IsNullOrWhiteSpace(filter) && startIndex.HasValue && count.HasValue)
            {
                userList = orgUsers.OrderBy(ou => ou.Email)
                    .Skip(startIndex.Value - 1)
                    .Take(count.Value)
                    .Select(ou => new ScimUserResponseModel(ou))
                    .ToList();
                totalResults = orgUsers.Count;
            }

            var result = new ScimListResponseModel<ScimUserResponseModel>
            {
                Resources = userList,
                ItemsPerPage = count.GetValueOrDefault(userList.Count),
                TotalResults = totalResults,
                StartIndex = startIndex.GetValueOrDefault(1),
            };

            return result;
        }
    }
}
