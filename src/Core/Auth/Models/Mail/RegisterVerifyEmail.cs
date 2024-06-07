using Bit.Core.Models.Mail;

namespace Bit.Core.Auth.Models.Mail;

public class RegisterVerifyEmail : BaseMailModel
{
    public string Url => string.Format("{0}/finish-signup?token={1}",
        WebVaultUrl,
        Token);

    public string Token { get; set; }
}
