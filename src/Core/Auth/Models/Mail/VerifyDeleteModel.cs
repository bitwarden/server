using Bit.Core.Models.Mail;

namespace Bit.Core.Auth.Models.Mail;

public class VerifyDeleteModel : BaseMailModel
{
    public string Url =>
        string.Format(
            "{0}/verify-recover-delete?userId={1}&token={2}&email={3}",
            WebVaultUrl,
            UserId,
            Token,
            EmailEncoded
        );

    public Guid UserId { get; set; }
    public string Email { get; set; }
    public string EmailEncoded { get; set; }
    public string Token { get; set; }
}
