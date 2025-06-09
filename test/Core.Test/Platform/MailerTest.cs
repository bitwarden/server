using Bit.Core.Auth.UserFeatures.Registration.VerifyEmail;
using Bit.Core.Platform.Services;
using Xunit;

namespace Bit.Core.Test.Platform;

public class MailerTest
{
    [Fact]
    public async Task SendEmailAsync()
    {
        var mailer = new Mailer(new HandlebarMailRenderer());

        var mail = new VerifyEmail
        {
            Token = "test-token",
            Email = "test@bitwarden.com",
            WebVaultUrl = "https://vault.bitwarden.com"
        };

        await mailer.SendEmail(mail, "test@bitwarden.com");
    }
}
