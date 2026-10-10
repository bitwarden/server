namespace Bit.Core.Dirt.Reports.Models.Data;

public class MemberAdoptionReportDetail
{
    public Guid OrganizationUserId { get; set; }
    public Guid? UserId { get; set; }
    public string? Name { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime? LastActivityDate { get; set; }
    public bool HasExtensionInstalled { get; set; }
    public int VaultItemCount { get; set; }
    public int SharedItemCount { get; set; }
    public bool HasRedeemedSponsorship { get; set; }
}
