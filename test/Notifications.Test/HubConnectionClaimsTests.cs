using System.Security.Claims;
using Bit.Core.Enums;
using Bit.Notifications;

namespace Notifications.Test;

public class HubConnectionClaimsTests
{
    private static readonly Guid _userId = Guid.Parse("d2ea5b72-6d47-4d20-b5a3-b7a6e89d8e7c");
    private static readonly Guid _organizationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void From_UserToken_ReadsUserClientTypeAndOrganizations()
    {
        var ownerOrg = Guid.NewGuid();
        var customOrg = Guid.NewGuid();
        var user = Principal(
            ("sub", _userId.ToString()),
            ("devicetype", nameof(DeviceType.ChromeExtension)),
            ("orgowner", ownerOrg.ToString()),
            ("orgadmin", _organizationId.ToString()),
            ("orguser", _organizationId.ToString()),
            ("orgcustom", customOrg.ToString()));

        var claims = HubConnectionClaims.From(user);

        Assert.Equal(_userId, claims.UserId);
        Assert.Equal(ClientType.Browser, claims.ClientType);
        Assert.Equal([ownerOrg, _organizationId, customOrg], claims.OrganizationIds);
    }

    [Fact]
    public void From_SendToken_IsEmpty()
    {
        var user = Principal(
            ("type", "Send"),
            ("sub", _userId.ToString()),
            ("devicetype", nameof(DeviceType.ChromeExtension)),
            ("orguser", _organizationId.ToString()));

        var claims = HubConnectionClaims.From(user);

        Assert.Null(claims.UserId);
        Assert.Equal(ClientType.All, claims.ClientType);
        Assert.Empty(claims.OrganizationIds);
    }

    [Fact]
    public void From_OrganizationApiKey_IsMemberOfThatOrganization()
    {
        var user = Principal(
            ("client_id", $"organization.{_organizationId}"),
            ("client_sub", _organizationId.ToString()));

        var claims = HubConnectionClaims.From(user);

        Assert.Null(claims.UserId);
        Assert.Equal([_organizationId], claims.OrganizationIds);
    }

    [Fact]
    public void From_InstallationApiKey_IsNotAnOrganizationMember()
    {
        var installationId = Guid.NewGuid();
        var user = Principal(
            ("client_id", $"installation.{installationId}"),
            ("client_sub", installationId.ToString()));

        var claims = HubConnectionClaims.From(user);

        Assert.Empty(claims.OrganizationIds);
    }

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "Bearer"));
    }
}
