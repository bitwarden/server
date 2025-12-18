using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationConfirmation;

#nullable enable

public class OrganizationConfirmationFamilyFreeView : OrganizationConfirmationBase
{
}

public class OrganizationConfirmationFamilyFree : BaseMail<OrganizationConfirmationFamilyFreeView>
{
    public override string Subject { get; set; } = "";
}
