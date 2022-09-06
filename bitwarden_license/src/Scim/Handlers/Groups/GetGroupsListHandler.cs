using Bit.Core.Repositories;
using Bit.Scim.Models;
using Bit.Scim.Queries.Groups;
using MediatR;

namespace Bit.Scim.Handlers.Groups;

public class GetGroupsListHandler : IRequestHandler<GetGroupsListQuery, ScimListResponseModel<ScimGroupResponseModel>>
{
    private readonly IGroupRepository _groupRepository;

    public GetGroupsListHandler(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<ScimListResponseModel<ScimGroupResponseModel>> Handle(GetGroupsListQuery request, CancellationToken cancellationToken)
    {
        string nameFilter = null;
        string externalIdFilter = null;
        if (!string.IsNullOrWhiteSpace(request.Filter))
        {
            if (request.Filter.StartsWith("displayName eq "))
            {
                nameFilter = request.Filter.Substring(15).Trim('"');
            }
            else if (request.Filter.StartsWith("externalId eq "))
            {
                externalIdFilter = request.Filter.Substring(14).Trim('"');
            }
        }

        var groupList = new List<ScimGroupResponseModel>();
        var groups = await _groupRepository.GetManyByOrganizationIdAsync(request.OrganizationId);
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
        else if (string.IsNullOrWhiteSpace(request.Filter) && request.StartIndex.HasValue && request.Count.HasValue)
        {
            groupList = groups.OrderBy(g => g.Name)
                .Skip(request.StartIndex.Value - 1)
                .Take(request.Count.Value)
                .Select(g => new ScimGroupResponseModel(g))
                .ToList();
            totalResults = groups.Count;
        }

        var result = new ScimListResponseModel<ScimGroupResponseModel>
        {
            Resources = groupList,
            ItemsPerPage = request.Count.GetValueOrDefault(groupList.Count),
            TotalResults = totalResults,
            StartIndex = request.StartIndex.GetValueOrDefault(1),
        };

        return result;
    }
}
