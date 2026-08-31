using Bit.Core.Pam.Models.Mail.AccessRequestPending;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Models.Mail.AccessRequestPending;

public class AccessRequestPendingViewTests
{
    private static readonly Guid _requestId = Guid.Parse("6d1f2b7c-0b8a-4a1e-9f0d-2f7b3c4d5e6a");

    /// <summary>The only spec that renders, so the only one that catches a misnamed or misplaced <c>.hbs</c>.</summary>
    [Fact]
    public async Task RenderAsync_CarriesTheRequesterWindowAndLinkInBothTemplates()
    {
        var (html, text) = await RenderAsync(View());

        foreach (var body in new[] { html, text })
        {
            Assert.Contains("requester@acme.com", body);
            Assert.Contains("Contoso", body);
            Assert.Contains("1 Sep 2026 at 08:30 UTC", body);
            Assert.Contains("1 Sep 2026 at 17:00 UTC", body);
            Assert.Contains($"https://vault.example.com/#/pam/requests/{_requestId}", body);
        }
    }

    [Fact]
    public async Task RenderAsync_EscapesTheOrganizationNameInTheHtmlBody()
    {
        var (html, _) = await RenderAsync(View(organizationName: "<script>alert(1)</script>"));

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Url_TargetsTheUserScopedRequestPage()
    {
        Assert.Equal(
            $"https://vault.example.com/#/pam/requests/{_requestId}",
            View().Url);
    }

    private static AccessRequestPendingView View(string organizationName = "Contoso") => new()
    {
        WebVaultUrl = "https://vault.example.com/#",
        AccessRequestId = _requestId,
        OrganizationName = organizationName,
        RequesterEmail = "requester@acme.com",
        NotBefore = new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Utc),
        NotAfter = new DateTime(2026, 9, 1, 17, 0, 0, DateTimeKind.Utc),
    };

    private static Task<(string html, string txt)> RenderAsync(BaseMailView view) =>
        new HandlebarMailRenderer(Substitute.For<ILogger<HandlebarMailRenderer>>(), new GlobalSettings())
            .RenderAsync(view);
}
