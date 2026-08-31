using System.Text.RegularExpressions;
using Bit.Core.Pam.Models.Mail.AccessRequestDecided;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Models.Mail.AccessRequestDecided;

public class AccessRequestDecidedViewTests
{
    private static readonly Guid _requestId = Guid.Parse("2c9a4f10-7b6e-4d33-9c21-5a8e0f1d3b47");

    /// <summary>The only spec that renders, so the only one that catches a misnamed or misplaced <c>.hbs</c>.</summary>
    [Fact]
    public async Task RenderAsync_Approved_SaysApprovedAndDoesNotClaimAccessHasStarted()
    {
        var (html, text) = await RenderAsync(View(approved: true));

        foreach (var body in new[] { Reflow(html), Reflow(text) })
        {
            Assert.Contains("was approved", body);
            Assert.Contains("has not started yet", body);
            Assert.Contains("cannot start it before the window above begins", body);
            Assert.Contains("Start access", body);
            Assert.Contains("Contoso", body);
            Assert.Contains("1 Sep 2026 at 08:30 UTC", body);
            Assert.Contains("1 Sep 2026 at 17:00 UTC", body);
            Assert.Contains($"https://vault.example.com/#/pam/requests/{_requestId}", body);
            Assert.DoesNotContain("declined", body);
            Assert.DoesNotContain("was denied", body);
        }
    }

    [Fact]
    public async Task RenderAsync_Denied_SaysDeclinedAndOffersNoAccessToStart()
    {
        var (html, text) = await RenderAsync(View(approved: false));

        foreach (var body in new[] { Reflow(html), Reflow(text) })
        {
            Assert.Contains("was denied", body);
            Assert.Contains("declined", body);
            Assert.Contains("No access was granted", body);
            Assert.Contains($"https://vault.example.com/#/pam/requests/{_requestId}", body);
            Assert.DoesNotContain("approved", body);
            Assert.DoesNotContain("Start access", body);
        }
    }

    /// <summary>The comment is free text that may name the system being accessed, so it is linked to, not rendered.</summary>
    [Fact]
    public void View_HasNoPlaceToCarryTheApproverComment() =>
        Assert.DoesNotContain(
            typeof(AccessRequestDecidedView).GetProperties(),
            property => property.Name.Contains("Comment", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Reason", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public async Task RenderAsync_EscapesTheOrganizationNameInTheHtmlBody()
    {
        var (html, _) = await RenderAsync(View(approved: true, organizationName: "<script>alert(1)</script>"));

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Url_TargetsTheRequestersOwnRequestPage() =>
        Assert.Equal(
            $"https://vault.example.com/#/pam/requests/{_requestId}",
            View(approved: true).Url);

    [Theory]
    [InlineData(true, "Your access request was approved")]
    [InlineData(false, "Your access request was denied")]
    public void Subject_CarriesTheVerdict(bool approved, string expected) =>
        Assert.Equal(expected, new AccessRequestDecidedMail("requester@acme.com", View(approved)).Subject);

    private static AccessRequestDecidedView View(bool approved, string organizationName = "Contoso") => new()
    {
        WebVaultUrl = "https://vault.example.com/#",
        AccessRequestId = _requestId,
        OrganizationName = organizationName,
        Approved = approved,
        NotBefore = new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Utc),
        NotAfter = new DateTime(2026, 9, 1, 17, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>Both templates wrap copy across source lines; collapsing whitespace keeps the specs about wording.</summary>
    private static string Reflow(string body) => Regex.Replace(body, @"\s+", " ");

    private static Task<(string html, string txt)> RenderAsync(BaseMailView view) =>
        new HandlebarMailRenderer(Substitute.For<ILogger<HandlebarMailRenderer>>(), new GlobalSettings())
            .RenderAsync(view);
}
