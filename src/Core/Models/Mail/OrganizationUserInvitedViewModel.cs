namespace Bit.Core.Models.Mail;

public class OrganizationUserInvitedViewModel : BaseMailModel
{
    public string OrganizationName { get; set; }
    public string OrganizationId { get; set; }
    public string OrganizationUserId { get; set; }
    public string Email { get; set; }
    public string OrganizationNameUrlEncoded { get; set; }
    public string Token { get; set; }
    public string ExpirationDate { get; set; }
    public string Url => string.Format("{0}/accept-organization?organizationId={1}&" +
        "organizationUserId={2}&email={3}&organizationName={4}&token={5}",
        WebVaultUrl,
        OrganizationId,
        OrganizationUserId,
        Email,
        OrganizationNameUrlEncoded,
        Token);
}
