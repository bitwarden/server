// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Mail;

namespace Bit.Core.Auth.Models.Mail;

public class VerifyEmailModel : BaseMailModel
{
    public string Url => string.Format("{0}/verify-email?userId={1}&token={2}",
        WebVaultUrl,
        UserId,
        Token);

    public Guid UserId { get; set; }
    public string Token { get; set; }
}
