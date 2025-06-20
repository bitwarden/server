using System.Net;
using Bit.Core.Platform.Services;

namespace Bit.Core.Test.Platform.TestMail;

public class TestMailView : BaseMailView
{
    public required string Token { get; init; }
    public required string Email { get; init; }
    public required string WebVaultUrl { get; init; }

    public string Url =>
        string.Format(
            "{0}/redirect-connector.html#finish-signup?token={1}&email={2}&fromEmail=true",
            WebVaultUrl,
            WebUtility.UrlEncode(Token),
            WebUtility.UrlEncode(Email)
        );
}

public class TestMail : BaseMail<TestMailView>
{
    public override string Subject { get; } = "Test Email";
}
