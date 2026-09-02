using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationUserAutoConfirmation;

public class OrganizationAutoConfirmationEnabledView : BaseMailView
{
    public required string WebVaultUrl { get; set; }
}

public class OrganizationAutoConfirmationEnabled : BaseMail<OrganizationAutoConfirmationEnabledView>
{
    public override required string Subject { get; set; }
}
