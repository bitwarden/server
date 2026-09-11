namespace Bit.Core.Dirt.Reports.Models.Data;

public class MemberAdoptionReportResult
{
    public int TotalMemberCount { get; set; }
    public int ActiveMemberCount { get; set; }
    public int InactiveMemberCount { get; set; }
    public int SponsoredFamiliesRedeemedCount { get; set; }
    public IEnumerable<MemberAdoptionReportMember> Members { get; set; } = [];
}

public class MemberAdoptionReportMember
{
    public Guid OrganizationUserId { get; set; }
    public Guid? UserId { get; set; }
    public string? Name { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool HasRecentLogin { get; set; }
    public bool HasExtensionInstalled { get; set; }
    public int VaultItemCount { get; set; }
    public int SharedItemCount { get; set; }
}
