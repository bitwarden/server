namespace Bit.Core.Models.Mail.Provider;

public class ProviderUserInvitedViewModel : BaseMailModel
{
    public string ProviderName { get; set; }
    public string ProviderId { get; set; }
    public string ProviderUserId { get; set; }
    public string Email { get; set; }
    public string ProviderNameUrlEncoded { get; set; }
    public string Token { get; set; }
    public string Url =>
        string.Format(
            "{0}/providers/accept-provider?providerId={1}&"
                + "providerUserId={2}&email={3}&providerName={4}&token={5}",
            WebVaultUrl,
            ProviderId,
            ProviderUserId,
            Email,
            ProviderNameUrlEncoded,
            Token
        );
}
