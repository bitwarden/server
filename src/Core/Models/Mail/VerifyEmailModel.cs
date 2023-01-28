namespace Bit.Core.Models.Mail;

public class VerifyEmailModel : BaseMailModel
{
    public string Url => string.Format("{0}/verify-email?userId={1}&token={2}",
        WebVaultUrl,
        UserId,
        Token);

    public Guid UserId { get; set; }
    public string Token { get; set; }
}
