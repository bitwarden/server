using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.AdminConsole.Models.Mail.Mailer.OrganizationInvite;

public abstract class OrganizationInviteBaseView : BaseMailView
{
    public required string OrganizationName { get; set; }
    public required string OrganizationId { get; set; }
    public required string OrganizationUserId { get; set; }
    public required string Email { get; set; }
    public required string Token { get; set; }
    public required string ExpirationDate { get; set; }
    public required string Url { get; set; }
    public required string ButtonText { get; set; }
    public required bool InitOrganization { get; set; }
    public string? InviterEmail { get; set; }
    public required string WebVaultUrl { get; set; }
    public required string TitleFirst { get; set; }
    public required string TitleSecondBold { get; set; }
    public required string TitleThird { get; set; }
}
