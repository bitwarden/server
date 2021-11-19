namespace Bit.Core.Models.Mail.FamiliesForEnterprise
{
    public class FamiliesForEnterpriseOfferExistingAccountViewModel : BaseMailModel
    {
        public string SponsorEmail { get; set; }
        public string SponsoredEmail { get; set; }
        public string SponsorshipToken { get; set; }
        public string Url => $"{WebVaultUrl}/?sponsorshipToken={SponsorshipToken}&email={SponsoredEmail}";
    }
}
