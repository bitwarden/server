using Bit.Core.Repositories;
using Bit.Scim.Models;
using Bit.Scim.Queries.Users;
using MediatR;

namespace Bit.Scim.Handlers.Users
{
    public class GetUsersListHandler : IRequestHandler<GetUsersListQuery, ScimListResponseModel<ScimUserResponseModel>>
    {
        private readonly IOrganizationUserRepository _organizationUserRepository;

        public GetUsersListHandler(IOrganizationUserRepository organizationUserRepository)
        {
            _organizationUserRepository = organizationUserRepository;
        }

        public async Task<ScimListResponseModel<ScimUserResponseModel>> Handle(GetUsersListQuery request, CancellationToken cancellationToken)
        {
            string emailFilter = null;
            string usernameFilter = null;
            string externalIdFilter = null;
            if (!string.IsNullOrWhiteSpace(request.Filter))
            {
                if (request.Filter.StartsWith("userName eq "))
                {
                    usernameFilter = request.Filter.Substring(12).Trim('"').ToLowerInvariant();
                    if (usernameFilter.Contains("@"))
                    {
                        emailFilter = usernameFilter;
                    }
                }
                else if (request.Filter.StartsWith("externalId eq "))
                {
                    externalIdFilter = request.Filter.Substring(14).Trim('"');
                }
            }

            var userList = new List<ScimUserResponseModel> { };
            var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(request.OrganizationId);
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
            else if (string.IsNullOrWhiteSpace(request.Filter) && request.StartIndex.HasValue && request.Count.HasValue)
            {
                userList = orgUsers.OrderBy(ou => ou.Email)
                    .Skip(request.StartIndex.Value - 1)
                    .Take(request.Count.Value)
                    .Select(ou => new ScimUserResponseModel(ou))
                    .ToList();
                totalResults = orgUsers.Count;
            }

            var result = new ScimListResponseModel<ScimUserResponseModel>
            {
                Resources = userList,
                ItemsPerPage = request.Count.GetValueOrDefault(userList.Count),
                TotalResults = totalResults,
                StartIndex = request.StartIndex.GetValueOrDefault(1),
            };

            return result;
        }
    }
}
