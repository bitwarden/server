using Bit.Core.Billing.Models.Mail.Mailer;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Models.Mail.Mailer;

public class PrivilegedControlsSeatLimitReachedViewTests
{
    private static readonly Guid _organizationId = Guid.Parse("6d1f2b7c-0b8a-4a1e-9f0d-2f7b3c4d5e6a");

    [Fact]
    public async Task RenderAsync_CarriesTheOrganizationLimitAndSubscriptionLinkInBothTemplates()
    {
        var (html, text) = await RenderAsync(View());

        foreach (var body in new[] { html, text })
        {
            Assert.Contains("Contoso", body);
            Assert.Contains("seat limit of 15", body);
            Assert.Contains($"https://vault.example.com/#/organizations/{_organizationId}/billing/subscription", body);
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
    public void VaultSubscriptionUrl_TargetsTheOrganizationSubscriptionPageOnTheConfiguredVault()
    {
        Assert.Equal(
            $"https://vault.example.com/#/organizations/{_organizationId}/billing/subscription",
            View().VaultSubscriptionUrl);
    }

    private static PrivilegedControlsSeatLimitReachedView View(string organizationName = "Contoso") =>
        new(new GlobalSettings { BaseServiceUri = { Vault = "https://vault.example.com" } })
        {
            OrganizationId = _organizationId,
            OrganizationName = organizationName,
            MaxSeatCount = 15
        };

    private static Task<(string html, string txt)> RenderAsync(BaseMailView view) =>
        new HandlebarMailRenderer(Substitute.For<ILogger<HandlebarMailRenderer>>(), new GlobalSettings())
            .RenderAsync(view);
}
