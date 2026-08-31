using Bit.Core.Pam.Models.Mail.AccessRequestsWaiting;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Models.Mail.AccessRequestsWaiting;

public class AccessRequestsWaitingViewTests
{
    /// <summary>
    /// <see cref="HandlebarMailRenderer" /> resolves both templates from the view's full class name and only fails
    /// when a mail is actually sent, which no other spec in this feature reaches. This is the spec that catches a
    /// misnamed or misplaced <c>.hbs</c>.
    /// </summary>
    [Fact]
    public async Task RenderAsync_CarriesTheCountWindowAndInboxLinkInBothTemplates()
    {
        var (html, text) = await RenderAsync(View());

        foreach (var body in new[] { html, text })
        {
            Assert.Contains("6 access requests have reached you in the last few minutes", body);
            Assert.Contains("for the rest of this 15-minute window", body);
            Assert.Contains("https://vault.example.com/#/pam/approvals", body);
        }
    }

    /// <summary>
    /// The collapsed mail stands in for requests it cannot enumerate, so it must not appear to name one — and the
    /// requester and window it omits are the fields the per-request mail carries.
    /// </summary>
    [Fact]
    public async Task RenderAsync_NamesNoIndividualRequest()
    {
        var (html, text) = await RenderAsync(View());

        foreach (var body in new[] { html, text })
        {
            Assert.DoesNotContain("pam/requests/", body);
        }
    }

    [Fact]
    public void Url_TargetsTheUserScopedApproverInbox()
    {
        Assert.Equal("https://vault.example.com/#/pam/approvals", View().Url);
    }

    private static AccessRequestsWaitingView View() => new()
    {
        WebVaultUrl = "https://vault.example.com/#",
        RequestCount = 6,
        WindowMinutes = 15,
    };

    private static Task<(string html, string txt)> RenderAsync(BaseMailView view) =>
        new HandlebarMailRenderer(Substitute.For<ILogger<HandlebarMailRenderer>>(), new GlobalSettings())
            .RenderAsync(view);
}
