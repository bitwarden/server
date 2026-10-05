using Bit.Core.Platform.Mail.Mailer;

namespace Bit.Core.Auth.Sso.Mail;

public class Saml2Rsa15DeprecationMailView : BaseMailView
{
    // ReSharper disable once MemberCanBeMadeStatic.Global
#pragma warning disable CA1822
    // Handlebars needs it to be an instance variable to work properly.
    public string DeprecationDate => "December 1, 2026";

    public string HelpPageUrl => "https://bitwarden.com/help/RSAES-PKCS1/";
#pragma warning restore CA1822
}

public class Saml2Rsa15DeprecationMail : BaseMail<Saml2Rsa15DeprecationMailView>
{
    public override string Subject { get; set; } = "Security alert: Update your identity provider's single sign-on encryption method";
}
