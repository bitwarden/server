using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationConfirmation;

public class OrganizationConfirmationFamilyFreeView : OrganizationConfirmationBaseView
{
}

public class OrganizationConfirmationFamilyFree : BaseMail<OrganizationConfirmationFamilyFreeView>
{
    public override required string Subject { get; set; }
}
