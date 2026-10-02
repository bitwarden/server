using Bit.Core.AdminConsole.Models.Mail.Mailer.MemberEmailChanged;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Models.Mail.Mailer.MemberEmailChanged;

public class MemberEmailChangedNotificationViewTests
{
    [Fact]
    public async Task RenderAsync_PopulatesNewEmailInBothTemplates()
    {
        var renderer = new HandlebarMailRenderer(
            Substitute.For<ILogger<HandlebarMailRenderer>>(),
            new GlobalSettings());

        var (html, text) = await renderer.RenderAsync(
            new MemberEmailChangedNotificationView { NewEmail = "new@acme.com" });

        Assert.Contains("new@acme.com", html);
        Assert.Contains("new@acme.com", text);
    }
}
