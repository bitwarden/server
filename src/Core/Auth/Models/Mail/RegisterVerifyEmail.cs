using Bit.Core.Models.Mail;

namespace Bit.Core.Auth.Models.Mail;

public class RegisterVerifyEmail : BaseMailModel
{
    // Note 1: We must include email in the URL even though it is already in the token so that the
    // client can use it to create the master key when they set their password.
    // We also have to include the fromEmail flag so that the client knows the user
    // is coming to the finish signup page from an email link and not directly from another route in the app.
    // Note 2: we cannot use a web vault url which contains a # as that is a reserved wild character on Android
    // so we must land on a redirect connector which will redirect to the finish signup page.
    // Note 3: The use of a fragment to indicate the redirect url is to prevent the query string from being logged by
    // proxies and servers. It also helps reduce open redirect vulnerabilities.
    public string Url =>
        string.Format(
            "{0}/redirect-connector.html#finish-signup?token={1}&email={2}&fromEmail=true",
            WebVaultUrl,
            Token,
            Email
        );

    public string Token { get; set; }
    public string Email { get; set; }
}
