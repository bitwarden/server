namespace Bit.Core.Models.Mail.FamiliesForEnterprise;

public class FamiliesForEnterpriseRemoveOfferViewModel : BaseMailModel
{
    public string SponsoringOrgName { get; set; }
    public string SponsoredOrganizationId { get; set; }
    public string OfferAcceptanceDate { get; set; }
    public string SubscriptionUrl =>
        $"{WebVaultUrl}/organizations/{SponsoredOrganizationId}/billing/subscription";
}
