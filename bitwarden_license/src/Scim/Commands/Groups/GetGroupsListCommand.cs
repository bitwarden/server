using Bit.Core.Repositories;
using Bit.Scim.Commands.Groups.Interfaces;
using Bit.Scim.Models;

namespace Bit.Scim.Commands.Groups
{
    public class GetGroupsListCommand : IGetGroupsListCommand
    {
        private readonly IGroupRepository _groupRepository;

        public GetGroupsListCommand(IGroupRepository groupRepository)
        {
            _groupRepository = groupRepository;
        }

        public async Task<ScimListResponseModel<ScimGroupResponseModel>> GetGroupsListAsync(Guid organizationId, string filter, int? count, int? startIndex)
        {
            string nameFilter = null;
            string externalIdFilter = null;
            if (!string.IsNullOrWhiteSpace(filter))
            {
                if (filter.StartsWith("displayName eq "))
                {
                    nameFilter = filter.Substring(15).Trim('"');
                }
                else if (filter.StartsWith("externalId eq "))
                {
                    externalIdFilter = filter.Substring(14).Trim('"');
                }
            }

            var groupList = new List<ScimGroupResponseModel>();
            var groups = await _groupRepository.GetManyByOrganizationIdAsync(organizationId);
            var totalResults = 0;
            if (!string.IsNullOrWhiteSpace(nameFilter))
            {
                var group = groups.FirstOrDefault(g => g.Name == nameFilter);
                if (group != null)
                {
                    groupList.Add(new ScimGroupResponseModel(group));
                }
                totalResults = groupList.Count;
            }
            else if (!string.IsNullOrWhiteSpace(externalIdFilter))
            {
                var group = groups.FirstOrDefault(ou => ou.ExternalId == externalIdFilter);
                if (group != null)
                {
                    groupList.Add(new ScimGroupResponseModel(group));
                }
                totalResults = groupList.Count;
            }
            else if (string.IsNullOrWhiteSpace(filter) && startIndex.HasValue && count.HasValue)
            {
                groupList = groups.OrderBy(g => g.Name)
                    .Skip(startIndex.Value - 1)
                    .Take(count.Value)
                    .Select(g => new ScimGroupResponseModel(g))
                    .ToList();
                totalResults = groups.Count;
            }

            var result = new ScimListResponseModel<ScimGroupResponseModel>
            {
                Resources = groupList,
                ItemsPerPage = count.GetValueOrDefault(groupList.Count),
                TotalResults = totalResults,
                StartIndex = startIndex.GetValueOrDefault(1),
            };

            return result;
        }
    }
}
