using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Settings;

namespace Bit.Core.Billing.Models.Mail.Mailer;

public class PrivilegedControlsSeatNeededView(IGlobalSettings globalSettings) : BaseMailView
{
    public required Guid OrganizationId { get; init; }

    public required string OrganizationName { get; init; }

    public required string MemberEmail { get; init; }

    public string VaultSubscriptionUrl =>
        $"{globalSettings.BaseServiceUri.VaultWithHash}/organizations/{OrganizationId}/billing/subscription";
}

public class PrivilegedControlsSeatNeededMail : BaseMail<PrivilegedControlsSeatNeededView>
{
    public override string Subject { get; set; } = "Privileged Controls seat needed";
}
