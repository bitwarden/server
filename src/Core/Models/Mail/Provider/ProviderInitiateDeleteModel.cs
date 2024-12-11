namespace Bit.Core.Models.Mail.Provider;

public class ProviderInitiateDeleteModel : BaseMailModel
{
    public string Url =>
        string.Format(
            "{0}/verify-recover-delete-provider?providerId={1}&token={2}&name={3}",
            WebVaultUrl,
            ProviderId,
            Token,
            ProviderNameUrlEncoded
        );

    public string Token { get; set; }
    public Guid ProviderId { get; set; }
    public string ProviderName { get; set; }
    public string ProviderNameUrlEncoded { get; set; }
    public string ProviderBillingEmail { get; set; }
    public string ProviderCreationDate { get; set; }
    public string ProviderCreationTime { get; set; }
    public string TimeZone { get; set; }
}
