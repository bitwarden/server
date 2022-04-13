namespace Bit.Core.Models.Mail.FamiliesForEnterprise
{
    public class FamiliesForEnterpriseOfferNewAccountViewModel : BaseMailModel
    {
        public string SponsorOrgName { get; set; }
        public string SponsoredEmail { get; set; }
        public string SponsorshipToken { get; set; }
        public string Url => $"{WebVaultUrl}/register?sponsorshipToken={SponsorshipToken}&email={SponsoredEmail}";
    }
}
