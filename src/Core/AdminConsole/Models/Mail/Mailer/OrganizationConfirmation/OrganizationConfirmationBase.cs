using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationConfirmation;


public abstract class OrganizationConfirmationBase : BaseMailView
{
    public required string OrganizationName { get; set; }
    public required string TitleFirst { get; set; }
    public required string TitleSecondBold { get; set; }
    public required string TitleThird { get; set; }
}
