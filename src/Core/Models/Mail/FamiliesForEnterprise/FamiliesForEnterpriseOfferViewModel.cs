namespace Bit.Core.Models.Mail.FamiliesForEnterprise;

public class FamiliesForEnterpriseOfferViewModel : BaseMailModel
{
    public string SponsorOrgName { get; set; }
    public string SponsoredEmail { get; set; }
    public string SponsorshipToken { get; set; }
    public bool ExistingAccount { get; set; }
    public bool SponsoringOrgId { get; set; }
    public string Url => string.Concat(
        WebVaultUrl,
        "/accept-families-for-enterprise",
        $"?token={SponsorshipToken}",
        $"&email={SponsoredEmail}",
        $"&orgId={SponsoringOrgId}",
        ExistingAccount ? "" : "&register=true"
    );
}
