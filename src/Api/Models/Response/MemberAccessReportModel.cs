namespace Api.Models.Response;

public class MemberAccessReportModel
{
    // public OrganizationUserUserDetailsResponseModel OrganizationMemberDetails { get; set; }
    // public IEnumerable<GroupDetailsResponseModel> MemberGroups { get; set; }
    // public IEnumerable<CollectionResponseModel> MemberCollections { get; set; }

    public string MemberName { get; set; }
    public string MemberEmail { get; set; }
    public int GroupCount { get; set; }
    public int CollectionCount { get; set; }
    public int ItemCount { get; set; }
    // public bool TwoFactorEnabled { get; set; }
    // public bool AccountRecovery { get; set; }  

}
