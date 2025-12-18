using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationConfirmation;

public class OrganizationConfirmationEnterpriseTeamsView : OrganizationConfirmationBase
{
}

public class OrganizationConfirmationEnterpriseTeams : BaseMail<OrganizationConfirmationEnterpriseTeamsView>
{
    public override string Subject { get; set; } = "";
}
