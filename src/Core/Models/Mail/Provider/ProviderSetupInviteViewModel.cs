namespace Bit.Core.Models.Mail.Provider;

public class ProviderSetupInviteViewModel : BaseMailModel
{
    public string ProviderId { get; set; }
    public string Email { get; set; }
    public string Token { get; set; }
    public string Url =>
        string.Format(
            "{0}/providers/setup-provider?providerId={1}&email={2}&token={3}",
            WebVaultUrl,
            ProviderId,
            Email,
            Token
        );
}
