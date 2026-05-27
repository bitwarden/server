using System.Net;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Models.Business;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Models.Mail;
using Xunit;

namespace Bit.Core.Test.Models.Mail;

public class OrganizationInvitesInfoTests
{
    private const string VaultWithHash = "https://vault.test/#";

    private static (OrganizationInvitesInfo Info, OrganizationUser OrgUser) CreateInfo(
        bool orgUserHasExistingUser = true,
        bool initOrganization = false,
        bool orgSsoEnabled = false,
        bool orgSsoLoginRequiredPolicyEnabled = false,
        string organizationName = "Acme & Co",
        string orgSsoIdentifier = "acme-sso",
        string email = "invitee+test@example.com",
        string protectedToken = "BwOrgUserInviteToken_abc/def=")
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = WebUtility.HtmlEncode(organizationName),
            Identifier = orgSsoIdentifier,
            PlanType = PlanType.EnterpriseAnnually
        };
        var orgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Email = email
        };
        var expiringToken = new ExpiringToken(protectedToken, DateTime.UtcNow.AddDays(5));

        var info = new OrganizationInvitesInfo(
            org,
            orgSsoEnabled,
            orgSsoLoginRequiredPolicyEnabled,
            new[] { (orgUser, expiringToken) },
            new Dictionary<Guid, bool> { [orgUser.Id] = orgUserHasExistingUser },
            initOrganization);

        return (info, orgUser);
    }

    [Fact]
    public void GetAcceptUrl_BuildsExpectedQueryParameters()
    {
        var (info, orgUser) = CreateInfo();

        var url = info.GetAcceptUrl(VaultWithHash, orgUser.Id);

        Assert.StartsWith($"{VaultWithHash}/accept-organization?", url);
        var query = url[(url.IndexOf('?') + 1)..];
        var pairs = query.Split('&');

        Assert.Equal($"organizationId={orgUser.OrganizationId}", pairs[0]);
        Assert.Equal($"organizationUserId={orgUser.Id}", pairs[1]);
        Assert.Equal($"email={WebUtility.UrlEncode(orgUser.Email)}", pairs[2]);
        Assert.Equal($"organizationName={WebUtility.UrlEncode("Acme & Co")}", pairs[3]);
        Assert.Equal($"token={WebUtility.UrlEncode("BwOrgUserInviteToken_abc/def=")}", pairs[4]);
        Assert.Equal("initOrganization=False", pairs[5]);
        Assert.Equal("orgUserHasExistingUser=True", pairs[6]);
        Assert.Equal(7, pairs.Length);
    }

    [Fact]
    public void GetAcceptUrl_UrlEncodesEmailOrgNameAndToken()
    {
        var (info, orgUser) = CreateInfo(
            email: "a b@example.com",
            organizationName: "A&B Org",
            protectedToken: "tok en/with+chars=");

        var url = info.GetAcceptUrl(VaultWithHash, orgUser.Id);

        Assert.Contains("email=a+b%40example.com", url);
        Assert.Contains("organizationName=A%26B+Org", url);
        Assert.Contains("token=tok+en%2Fwith%2Bchars%3D", url);
    }

    [Fact]
    public void GetAcceptUrl_OmitsOrgSsoIdentifier_WhenSsoDisabled()
    {
        var (info, orgUser) = CreateInfo(orgSsoEnabled: false, orgSsoLoginRequiredPolicyEnabled: true);

        var url = info.GetAcceptUrl(VaultWithHash, orgUser.Id);

        Assert.DoesNotContain("orgSsoIdentifier", url);
    }

    [Fact]
    public void GetAcceptUrl_OmitsOrgSsoIdentifier_WhenRequireSsoPolicyDisabled()
    {
        var (info, orgUser) = CreateInfo(orgSsoEnabled: true, orgSsoLoginRequiredPolicyEnabled: false);

        var url = info.GetAcceptUrl(VaultWithHash, orgUser.Id);

        Assert.DoesNotContain("orgSsoIdentifier", url);
    }

    [Fact]
    public void GetAcceptUrl_IncludesOrgSsoIdentifier_WhenSsoEnabledAndRequired()
    {
        var (info, orgUser) = CreateInfo(
            orgSsoEnabled: true,
            orgSsoLoginRequiredPolicyEnabled: true,
            orgSsoIdentifier: "acme-sso");

        var url = info.GetAcceptUrl(VaultWithHash, orgUser.Id);

        Assert.EndsWith("&orgSsoIdentifier=acme-sso", url);
    }

    [Fact]
    public void GetAcceptUrl_RendersInitOrganizationAndExistingUserFlags()
    {
        var (info, orgUser) = CreateInfo(orgUserHasExistingUser: false, initOrganization: true);

        var url = info.GetAcceptUrl(VaultWithHash, orgUser.Id);

        Assert.Contains("initOrganization=True", url);
        Assert.Contains("orgUserHasExistingUser=False", url);
    }

    [Fact]
    public void Constructor_HtmlDecodesOrganizationName()
    {
        // Org.Name is stored HTML-encoded in the database; consumers (mail subject, URL) need it decoded.
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = WebUtility.HtmlEncode("R&D <Team>"),
            PlanType = PlanType.EnterpriseAnnually
        };

        var info = new OrganizationInvitesInfo(
            org, false, false,
            Array.Empty<(OrganizationUser, ExpiringToken)>(),
            new Dictionary<Guid, bool>());

        Assert.Equal("R&D <Team>", info.OrganizationName);
    }

    [Fact]
    public void Constructor_SetsIsFreeOrg_OnlyWhenPlanTypeIsFree()
    {
        var freeOrg = new Organization { PlanType = PlanType.Free };
        var paidOrg = new Organization { PlanType = PlanType.EnterpriseAnnually };
        var pairs = Array.Empty<(OrganizationUser, ExpiringToken)>();
        var dict = new Dictionary<Guid, bool>();

        var free = new OrganizationInvitesInfo(freeOrg, false, false, pairs, dict);
        var paid = new OrganizationInvitesInfo(paidOrg, false, false, pairs, dict);

        Assert.True(free.IsFreeOrg);
        Assert.Equal(PlanType.Free, free.PlanType);
        Assert.False(paid.IsFreeOrg);
        Assert.Equal(PlanType.EnterpriseAnnually, paid.PlanType);
    }

    [Fact]
    public void Constructor_AppliesOptionalArgumentDefaults()
    {
        var org = new Organization { PlanType = PlanType.EnterpriseAnnually };

        var info = new OrganizationInvitesInfo(
            org, false, false,
            Array.Empty<(OrganizationUser, ExpiringToken)>(),
            new Dictionary<Guid, bool>());

        Assert.False(info.InitOrganization);
        Assert.Null(info.InviterEmail);
    }

    [Fact]
    public void Constructor_StoresPassedArgumentsOnProperties()
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            Identifier = "acme-sso",
            PlanType = PlanType.EnterpriseAnnually
        };
        var orgUser = new OrganizationUser { Id = Guid.NewGuid(), OrganizationId = org.Id };
        var token = new ExpiringToken("tok", DateTime.UtcNow.AddDays(5));
        var pairs = new[] { (orgUser, token) };
        var dict = new Dictionary<Guid, bool> { [orgUser.Id] = true };

        var info = new OrganizationInvitesInfo(
            org,
            orgSsoEnabled: true,
            orgSsoLoginRequiredPolicyEnabled: true,
            orgUserTokenPairs: pairs,
            orgUserHasExistingUserDict: dict,
            initOrganization: true,
            inviterEmail: "admin@acme.test");

        Assert.True(info.OrgSsoEnabled);
        Assert.True(info.OrgSsoLoginRequiredPolicyEnabled);
        Assert.Equal("acme-sso", info.OrgSsoIdentifier);
        Assert.True(info.InitOrganization);
        Assert.Equal("admin@acme.test", info.InviterEmail);
        Assert.Same(pairs, info.OrgUserTokenPairs);
        Assert.Same(dict, info.OrgUserHasExistingUserDict);
    }
}
