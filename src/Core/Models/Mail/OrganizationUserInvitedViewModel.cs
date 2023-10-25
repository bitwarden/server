namespace Bit.Core.Models.Mail;

public class OrganizationUserInvitedViewModel : BaseTitleContactUsMailModel
{
    public string OrganizationName { get; set; }
    public string OrganizationId { get; set; }
    public string OrganizationUserId { get; set; }
    public string Email { get; set; }
    public string OrganizationNameUrlEncoded { get; set; }
    public string Token { get; set; }
    public string ExpirationDate { get; set; }
    public bool InitOrganization { get; set; }
    public string OrgSsoIdentifier { get; set; }
    public bool OrgSsoEnabled { get; set; }
    public bool OrgSsoLoginRequiredPolicyEnabled { get; set; }
    public bool OrgUserHasExistingUser { get; set; }

    public string Url => string.Format("{0}/accept-organization?organizationId={1}&" +
        "organizationUserId={2}&email={3}&organizationName={4}&token={5}&initOrganization={6}" +
        "&orgSsoIdentifier={7}&orgSsoEnabled={8}&orgSsoLoginRequiredPolicyEnabled={9}&orgUserHasExistingUser={10}",
        WebVaultUrl,
        OrganizationId,
        OrganizationUserId,
        Email,
        OrganizationNameUrlEncoded,
        Token,
        InitOrganization,
        OrgSsoIdentifier,
        OrgSsoEnabled,
        OrgSsoLoginRequiredPolicyEnabled,
        OrgUserHasExistingUser
        );
}
