// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail;

public class PamPendingAccessRequestViewModel : BaseMailModel
{
    public string OrganizationName { get; set; }
    public string RequesterName { get; set; }
    public string RequesterEmail { get; set; }
    public string NotBefore { get; set; }
    public string NotAfter { get; set; }
    public string Reason { get; set; }
    public string ApproverInboxUrl => $"{WebVaultUrl}/pam/approver-inbox/approvals";
}
