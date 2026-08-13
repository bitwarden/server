using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationInvite;

public abstract class OrganizationInviteBaseView : BaseMailView
{
    public required string OrganizationName { get; set; }
    public required string Email { get; set; }
    public required string ExpirationDate { get; set; }
    public required string Url { get; set; }
    public required string ButtonText { get; set; }
    public string? InviterEmail { get; set; }
}
