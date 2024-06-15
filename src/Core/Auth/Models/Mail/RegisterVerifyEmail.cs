using Bit.Core.Models.Mail;

namespace Bit.Core.Auth.Models.Mail;

public class RegisterVerifyEmail : BaseMailModel
{
    // We must include email in the URL even though it is already in the token so that the
    // client can use it to create the master key when they set their password.
    // We also have to include the fromEmail flag so that the client knows the user
    // is coming to the finish signup page from an email link and not directly from another route in the app.
    public string Url => string.Format("{0}/finish-signup?token={1}&email={2}&fromEmail=true",
        WebVaultUrl,
        Token,
        Email);

    public string Token { get; set; }
    public string Email { get; set; }
}
