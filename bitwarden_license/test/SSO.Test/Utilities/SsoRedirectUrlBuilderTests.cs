using Bit.Sso.Utilities;

namespace Bit.SSO.Test.Utilities;

public class SsoRedirectUrlBuilderTests
{
    // VaultWithHash format mirrors GlobalSettings.BaseServiceUri.VaultWithHash:
    // "{Vault}/#" — the Angular hash router parses everything after the "#".
    private const string VaultWithHash = "https://vault.bitwarden.com/#";
    private const string InviteAcceptanceRequired =
        SsoRedirectUrlBuilder.ErrorCodes.InviteAcceptanceRequired;

    [Fact]
    public void BuildLoginRedirectUrl_BasicInputs_ComposesExpectedUrl()
    {
        var url = SsoRedirectUrlBuilder.BuildLoginRedirectUrl(
            VaultWithHash,
            email: "user@example.com",
            organizationDisplayName: "Acme",
            errorCode: InviteAcceptanceRequired);

        Assert.Equal(
            "https://vault.bitwarden.com/#/login?email=user%40example.com" +
            "&organizationName=Acme" +
            "&error=ssoOrgInviteAcceptanceRequired",
            url);
    }

    [Fact]
    public void BuildLoginRedirectUrl_PlusInEmail_EncodesPlus()
    {
        // `+` is a valid local-part character; `Uri.EscapeDataString` must encode it
        // so the receiving client sees the literal "+" after decoding rather than
        // a space (the application/x-www-form-urlencoded interpretation).
        var url = SsoRedirectUrlBuilder.BuildLoginRedirectUrl(
            VaultWithHash,
            email: "user+tag@example.com",
            organizationDisplayName: "Acme",
            errorCode: InviteAcceptanceRequired);

        Assert.Contains("email=user%2Btag%40example.com", url);
    }

    [Fact]
    public void BuildLoginRedirectUrl_SpacesInOrgName_EncodesSpaces()
    {
        var url = SsoRedirectUrlBuilder.BuildLoginRedirectUrl(
            VaultWithHash,
            email: "user@example.com",
            organizationDisplayName: "Acme Corp",
            errorCode: InviteAcceptanceRequired);

        // Uri.EscapeDataString encodes spaces as %20 (not '+'), which is what we want
        // for an Angular router-parsed hash query string.
        Assert.Contains("organizationName=Acme%20Corp", url);
    }

    [Fact]
    public void BuildLoginRedirectUrl_AmpersandInOrgName_EncodesAmpersand()
    {
        // An unencoded `&` would terminate the organizationName param prematurely
        // and inject a phantom param on the receiving end. This must be encoded.
        var url = SsoRedirectUrlBuilder.BuildLoginRedirectUrl(
            VaultWithHash,
            email: "user@example.com",
            organizationDisplayName: "Acme & Co",
            errorCode: InviteAcceptanceRequired);

        Assert.Contains("organizationName=Acme%20%26%20Co", url);
        Assert.DoesNotContain("Acme & Co", url);
    }

    [Fact]
    public void BuildLoginRedirectUrl_PercentInOrgName_EncodesPercent()
    {
        var url = SsoRedirectUrlBuilder.BuildLoginRedirectUrl(
            VaultWithHash,
            email: "user@example.com",
            organizationDisplayName: "50% Off Inc",
            errorCode: InviteAcceptanceRequired);

        Assert.Contains("organizationName=50%25%20Off%20Inc", url);
    }

    [Fact]
    public void BuildLoginRedirectUrl_UnicodeInOrgName_EncodesUtf8Bytes()
    {
        // Org display names can contain Unicode; Uri.EscapeDataString emits the
        // percent-encoded UTF-8 byte sequence for non-ASCII code points.
        var url = SsoRedirectUrlBuilder.BuildLoginRedirectUrl(
            VaultWithHash,
            email: "user@example.com",
            organizationDisplayName: "Café Münchën 漢字",
            errorCode: InviteAcceptanceRequired);

        // U+00E9 (é) → %C3%A9, U+00FC (ü) → %C3%BC, etc. We assert the round-trip
        // rather than the byte-exact encoding to keep the test stable against
        // normalization choices.
        var query = url.Substring(url.IndexOf("organizationName=", StringComparison.Ordinal)
                                  + "organizationName=".Length);
        var orgValue = query.Split('&')[0];
        Assert.Equal("Café Münchën 漢字", Uri.UnescapeDataString(orgValue));
    }

    [Fact]
    public void BuildLoginRedirectUrl_ErrorCodeIsAppendedRaw()
    {
        // Error codes are server-controlled constants (no user input), so they
        // are appended as-is. Asserting that ensures the contract with the
        // web client's string match stays stable.
        var url = SsoRedirectUrlBuilder.BuildLoginRedirectUrl(
            VaultWithHash,
            email: "user@example.com",
            organizationDisplayName: "Acme",
            errorCode: InviteAcceptanceRequired);

        Assert.EndsWith("&error=ssoOrgInviteAcceptanceRequired", url);
    }

    [Fact]
    public void BuildLoginRedirectUrl_AutoSubmitDefault_OmitsAutoSubmitParam()
    {
        var url = SsoRedirectUrlBuilder.BuildLoginRedirectUrl(
            VaultWithHash,
            email: "user@example.com",
            organizationDisplayName: "Acme",
            errorCode: InviteAcceptanceRequired);

        Assert.DoesNotContain("autoSubmit", url);
    }

    [Fact]
    public void BuildLoginRedirectUrl_AutoSubmitTrue_AppendsAutoSubmitParam()
    {
        var url = SsoRedirectUrlBuilder.BuildLoginRedirectUrl(
            VaultWithHash,
            email: "user@example.com",
            organizationDisplayName: "Acme",
            errorCode: InviteAcceptanceRequired,
            autoSubmit: true);

        Assert.EndsWith("&autoSubmit=true", url);
    }

    [Fact]
    public void ErrorCodes_InviteAcceptanceRequired_HasStableValue()
    {
        // This value is a cross-language contract with the web client's
        // SsoRedirectErrorCode.InviteAcceptanceRequired. Changing it requires
        // a coordinated client change.
        Assert.Equal(
            "ssoOrgInviteAcceptanceRequired",
            SsoRedirectUrlBuilder.ErrorCodes.InviteAcceptanceRequired);
    }
}
