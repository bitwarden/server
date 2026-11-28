using Bit.RustSDK;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Crypto;

/// <summary>
/// Covers the C# wrapper around the Rust <c>make_organization_invite</c> export. The Rust tests prove the invite
/// secret and the organization key both open the sealed invite; these prove the wrapper returns both parts and
/// surfaces Rust errors as <see cref="RustSdkException"/>.
/// </summary>
public class OrganizationInviteCryptoTests
{
    [Fact]
    public void MakeOrganizationInvite_ReturnsSealedInviteAndUrlSafeSecret()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        var invite = RustSdkService.MakeOrganizationInvite(orgKeys.Key, orgKeys.PrivateKey);

        Assert.False(string.IsNullOrEmpty(invite.Invite));
        Assert.False(string.IsNullOrEmpty(invite.Secret));
        Assert.Matches("^[A-Za-z0-9_=-]+$", invite.Secret);
        Assert.DoesNotContain(invite.Secret, invite.Invite);
    }

    [Fact]
    public void MakeOrganizationInvite_MakesANewSecretEachTime()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        var first = RustSdkService.MakeOrganizationInvite(orgKeys.Key, orgKeys.PrivateKey);
        var second = RustSdkService.MakeOrganizationInvite(orgKeys.Key, orgKeys.PrivateKey);

        Assert.NotEqual(first.Secret, second.Secret);
        Assert.NotEqual(first.Invite, second.Invite);
    }

    [Fact]
    public void MakeOrganizationInvite_PrivateKeyFromAnotherOrganization_Throws()
    {
        var orgKeys = RustSdkService.GenerateOrganizationKeys();
        var otherOrgKeys = RustSdkService.GenerateOrganizationKeys();

        Assert.Throws<RustSdkException>(() =>
            RustSdkService.MakeOrganizationInvite(orgKeys.Key, otherOrgKeys.PrivateKey));
    }
}
