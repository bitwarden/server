namespace Bit.Core.Models.Mail;

public class OrganizationUserInitInvitedViewModel : OrganizationUserInvitedViewModel
{
    public new string Url => string.Format("{0}/accept-init-organization?organizationId={1}&" +
        "organizationUserId={2}&email={3}&organizationName={4}&token={5}",
        WebVaultUrl,
        OrganizationId,
        OrganizationUserId,
        Email,
        OrganizationNameUrlEncoded,
        Token);
}
