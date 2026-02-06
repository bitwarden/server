// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail.FamiliesForEnterprise;

public class FamiliesForEnterpriseOfferViewModel : BaseMailModel
{
    public string SponsorOrgName { get; set; }
    public string SponsoredEmail { get; set; }
    public string SponsorshipToken { get; set; }
    public bool ExistingAccount { get; set; }
    public string Url => string.Concat(
        WebVaultUrl,
        "/accept-families-for-enterprise",
        $"?token={SponsorshipToken}",
        $"&email={SponsoredEmail}",
        ExistingAccount ? "" : "&register=true"
    );
}
