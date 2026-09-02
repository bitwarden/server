// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Mail;

namespace Bit.Core.AdminConsole.Models.Mail;

public class DeviceApprovalRequestedViewModel : BaseMailModel
{
    public Guid OrganizationId { get; set; }
    public string UserNameRequestingAccess { get; set; }

    public string Url => string.Format("{0}/organizations/{1}/settings/device-approvals",
        WebVaultUrl,
        OrganizationId);
}

