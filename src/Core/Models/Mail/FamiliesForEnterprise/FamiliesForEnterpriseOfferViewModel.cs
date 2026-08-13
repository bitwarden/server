// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Mail.FamiliesForEnterprise;

public class FamiliesForEnterpriseOfferViewModel : BaseMailModel
{
    public string SponsorOrgName { get; set; }
    public string SponsoredEmail { get; set; }
    public string SponsorshipToken { get; set; }
    public bool ExistingAccount { get; set; }
    /// <summary>
    /// Gates the updated "Sponsored Families Plan" copy behind the
    /// <see cref="FeatureFlagKeys.VFO1Foundation"/> feature flag. When <c>false</c>, the template falls
    /// back to the original copy.
    /// </summary>
    public bool VFO1FoundationEnabled { get; set; }
    public string Url => string.Concat(
        WebVaultUrl,
        "/accept-families-for-enterprise",
        $"?token={SponsorshipToken}",
        $"&email={SponsoredEmail}",
        ExistingAccount ? "" : "&register=true"
    );
}
