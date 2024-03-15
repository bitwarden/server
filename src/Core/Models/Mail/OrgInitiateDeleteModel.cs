using Bit.Core.Models.Mail;

namespace Bit.Core.Auth.Models.Mail;

public class OrgInitiateDeleteModel : BaseMailModel
{
    public string Url => string.Format("{0}/verify-org-delete?orgId={1}&token={2}",
        WebVaultUrl,
        OrganizationId,
        Token);

    public string Token { get; set; }
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; }
    public string OrganizationPlan { get; set; }
    public string OrganizationSeats { get; set; }
    public string OrganizationBillingEmail { get; set; }
    public string OrganizationCreationDate { get; set; }
    public string OrganizationCreationTime { get; set; }
    public string TimeZone { get; set; }
}
