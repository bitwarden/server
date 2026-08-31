using System.Text.RegularExpressions;
using Bit.Core.Pam.Models.Mail.AccessLeaseRevoked;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Models.Mail.AccessLeaseRevoked;

public class AccessLeaseRevokedViewTests
{
    private static readonly Guid _requestId = Guid.Parse("6f1b2d84-0c37-4a91-8e55-1d7c93a4b208");

    /// <summary>The only spec that renders, so the only one that catches a misnamed or misplaced <c>.hbs</c>.</summary>
    [Fact]
    public async Task RenderAsync_SaysAccessWasRevokedAndPointsAtTheRequest()
    {
        var (html, text) = await RenderAsync(View());

        foreach (var body in new[] { Reflow(html), Reflow(text) })
        {
            Assert.Contains("Your access was revoked", body);
            Assert.Contains("which was due to run until", body);
            Assert.Contains("Contoso", body);
            Assert.Contains("1 Sep 2026 at 17:00 UTC", body);
            Assert.Contains($"https://vault.example.com/#/pam/requests/{_requestId}", body);
        }
    }

    /// <summary>A revoked lease is over; copy implying otherwise sends the holder to a button that cannot help.</summary>
    [Fact]
    public async Task RenderAsync_SaysTheAccessCannotBeResumedAndThatANewRequestIsNeeded()
    {
        var (html, text) = await RenderAsync(View());

        foreach (var body in new[] { Reflow(html), Reflow(text) })
        {
            Assert.Contains("cannot be resumed", body);
            Assert.Contains("ask for it again with a new request", body);
            Assert.DoesNotContain("Start access", body);
            Assert.DoesNotContain("Resume", body);
        }
    }

    /// <summary>The reason is free text that may name the system being accessed, so it is linked to, not rendered.</summary>
    [Fact]
    public void View_HasNoPlaceToCarryTheRevocationReason() =>
        Assert.DoesNotContain(
            typeof(AccessLeaseRevokedView).GetProperties(),
            property => property.Name.Contains("Reason", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Comment", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Detail", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public async Task RenderAsync_EscapesTheOrganizationNameInTheHtmlBody()
    {
        var (html, _) = await RenderAsync(View(organizationName: "<script>alert(1)</script>"));

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Url_TargetsTheHoldersOwnRequestPage() =>
        Assert.Equal($"https://vault.example.com/#/pam/requests/{_requestId}", View().Url);

    [Fact]
    public void Subject_SaysTheAccessWasRevokedWithoutNamingTheItemOrTheReason() =>
        Assert.Equal(
            "Your access was revoked",
            new AccessLeaseRevokedMail { ToEmails = ["holder@acme.com"], View = View() }.Subject);

    private static AccessLeaseRevokedView View(string organizationName = "Contoso") => new()
    {
        WebVaultUrl = "https://vault.example.com/#",
        AccessRequestId = _requestId,
        OrganizationName = organizationName,
        NotAfter = new DateTime(2026, 9, 1, 17, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>Both templates wrap copy across source lines; collapsing whitespace keeps the specs about wording.</summary>
    private static string Reflow(string body) => Regex.Replace(body, @"\s+", " ");

    private static Task<(string html, string txt)> RenderAsync(BaseMailView view) =>
        new HandlebarMailRenderer(Substitute.For<ILogger<HandlebarMailRenderer>>(), new GlobalSettings())
            .RenderAsync(view);
}
