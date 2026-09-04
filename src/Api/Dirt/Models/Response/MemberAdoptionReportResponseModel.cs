using Bit.Core.Dirt.Reports.Models.Data;

namespace Bit.Api.Dirt.Models.Response;

public class MemberAdoptionReportResponseModel
{
    public int TotalMemberCount { get; set; }
    public int ActiveMemberCount { get; set; }
    public int InactiveMemberCount { get; set; }
    public int SponsoredFamiliesRedeemedCount { get; set; }
    public IEnumerable<MemberAdoptionReportMemberResponseModel> Members { get; set; } = [];

    public MemberAdoptionReportResponseModel(MemberAdoptionReportResult result)
    {
        TotalMemberCount = result.TotalMemberCount;
        ActiveMemberCount = result.ActiveMemberCount;
        InactiveMemberCount = result.InactiveMemberCount;
        SponsoredFamiliesRedeemedCount = result.SponsoredFamiliesRedeemedCount;
        Members = result.Members.Select(member => new MemberAdoptionReportMemberResponseModel(member));
    }
}

public class MemberAdoptionReportMemberResponseModel
{
    public Guid OrganizationUserId { get; set; }
    public Guid? UserId { get; set; }
    public string? Name { get; set; }
    public string Email { get; set; }
    public bool HasRecentLogin { get; set; }
    public bool HasExtensionInstalled { get; set; }
    public int VaultItemCount { get; set; }
    public int SharedItemCount { get; set; }

    public MemberAdoptionReportMemberResponseModel(MemberAdoptionReportMember member)
    {
        OrganizationUserId = member.OrganizationUserId;
        UserId = member.UserId;
        Name = member.Name;
        Email = member.Email;
        HasRecentLogin = member.HasRecentLogin;
        HasExtensionInstalled = member.HasExtensionInstalled;
        VaultItemCount = member.VaultItemCount;
        SharedItemCount = member.SharedItemCount;
    }
}
