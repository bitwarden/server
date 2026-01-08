using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationConfirmation;

public class OrganizationConfirmationEnterpriseTeamsView : OrganizationConfirmationBaseView
{
}

public class OrganizationConfirmationEnterpriseTeams : BaseMail<OrganizationConfirmationEnterpriseTeamsView>
{
    public override required string Subject { get; set; }
}
