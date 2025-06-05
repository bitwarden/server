using System.Net;
using Bit.Core.Platform.Services;

namespace Bit.Core.Auth.UserFeatures.Registration.VerifyEmail;

public class VerifyEmail() : BaseMailModel2
{
    public override string Subject { get; set; } = "Verify Your Email";

    public required string Token { get; init; }
    public required string Email { get; init; }
    public required string WebVaultUrl { get; init; }

    public string Url
    {
        get => string.Format(
            "{0}/redirect-connector.html#finish-signup?token={1}&email={2}&fromEmail=true",
            WebVaultUrl,
            WebUtility.UrlEncode(Token),
            WebUtility.UrlEncode(Email)
        );
    }

}
