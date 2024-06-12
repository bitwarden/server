using Bit.Api.AdminConsole.Models.Response;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Models.Public.Response;

namespace Api.Models.Response;

public class MemberAccessReportModel
{
    public OrganizationUserDetailsResponseModel OrganizationMemberDetails { get; set; }
    public IEnumerable<GroupDetailsResponseModel> MemberGroups { get; set; }
    public IEnumerable<CollectionResponseModel> MemberCollections { get; set; }
}
