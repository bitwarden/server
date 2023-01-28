using Bit.Core.Enums;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Bit.Core.Models.Data;

public class SsoConfigurationData
{
    private static string _oidcSigninPath = "/oidc-signin";
    private static string _oidcSignedOutPath = "/oidc-signedout";
    private static string _saml2ModulePath = "/saml2";

    public static SsoConfigurationData Deserialize(string data)
    {
        return CoreHelpers.LoadClassFromJsonData<SsoConfigurationData>(data);
    }

    public string Serialize()
    {
        return CoreHelpers.ClassToJsonData(this);
    }

    public SsoType ConfigType { get; set; }

    public bool KeyConnectorEnabled { get; set; }
    public string KeyConnectorUrl { get; set; }

    // OIDC
    public string Authority { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string MetadataAddress { get; set; }
    public OpenIdConnectRedirectBehavior RedirectBehavior { get; set; }
    public bool GetClaimsFromUserInfoEndpoint { get; set; }
    public string AdditionalScopes { get; set; }
    public string AdditionalUserIdClaimTypes { get; set; }
    public string AdditionalEmailClaimTypes { get; set; }
    public string AdditionalNameClaimTypes { get; set; }
    public string AcrValues { get; set; }
    public string ExpectedReturnAcrValue { get; set; }

    // SAML2 IDP
    public string IdpEntityId { get; set; }
    public string IdpSingleSignOnServiceUrl { get; set; }
    public string IdpSingleLogoutServiceUrl { get; set; }
    public string IdpX509PublicCert { get; set; }
    public Saml2BindingType IdpBindingType { get; set; }
    public bool IdpAllowUnsolicitedAuthnResponse { get; set; }
    public string IdpArtifactResolutionServiceUrl { get => null; set { /*IGNORE*/ } }
    public bool IdpDisableOutboundLogoutRequests { get; set; }
    public string IdpOutboundSigningAlgorithm { get; set; }
    public bool IdpWantAuthnRequestsSigned { get; set; }

    // SAML2 SP
    public Saml2NameIdFormat SpNameIdFormat { get; set; }
    public string SpOutboundSigningAlgorithm { get; set; }
    public Saml2SigningBehavior SpSigningBehavior { get; set; }
    public bool SpWantAssertionsSigned { get; set; }
    public bool SpValidateCertificates { get; set; }
    public string SpMinIncomingSigningAlgorithm { get; set; }

    public static string BuildCallbackPath(string ssoUri = null)
    {
        return BuildSsoUrl(_oidcSigninPath, ssoUri);
    }

    public static string BuildSignedOutCallbackPath(string ssoUri = null)
    {
        return BuildSsoUrl(_oidcSignedOutPath, ssoUri);
    }

    public static string BuildSaml2ModulePath(string ssoUri = null, string scheme = null)
    {
        return string.Concat(BuildSsoUrl(_saml2ModulePath, ssoUri),
            string.IsNullOrWhiteSpace(scheme) ? string.Empty : $"/{scheme}");
    }

    public static string BuildSaml2AcsUrl(string ssoUri = null, string scheme = null)
    {
        return string.Concat(BuildSaml2ModulePath(ssoUri, scheme), "/Acs");
    }

    public static string BuildSaml2MetadataUrl(string ssoUri = null, string scheme = null)
    {
        return BuildSaml2ModulePath(ssoUri, scheme);
    }

    public IEnumerable<string> GetAdditionalScopes() => AdditionalScopes?
        .Split(',')?
        .Where(c => !string.IsNullOrWhiteSpace(c))?
        .Select(c => c.Trim()) ??
        Array.Empty<string>();

    public IEnumerable<string> GetAdditionalUserIdClaimTypes() => AdditionalUserIdClaimTypes?
        .Split(',')?
        .Where(c => !string.IsNullOrWhiteSpace(c))?
        .Select(c => c.Trim()) ??
        Array.Empty<string>();

    public IEnumerable<string> GetAdditionalEmailClaimTypes() => AdditionalEmailClaimTypes?
        .Split(',')?
        .Where(c => !string.IsNullOrWhiteSpace(c))?
        .Select(c => c.Trim()) ??
        Array.Empty<string>();

    public IEnumerable<string> GetAdditionalNameClaimTypes() => AdditionalNameClaimTypes?
        .Split(',')?
        .Where(c => !string.IsNullOrWhiteSpace(c))?
        .Select(c => c.Trim()) ??
        Array.Empty<string>();

    private static string BuildSsoUrl(string relativePath, string ssoUri)
    {
        if (string.IsNullOrWhiteSpace(ssoUri) ||
            !Uri.IsWellFormedUriString(ssoUri, UriKind.Absolute))
        {
            return relativePath;
        }
        if (Uri.TryCreate(string.Concat(ssoUri.TrimEnd('/'), relativePath), UriKind.Absolute, out var newUri))
        {
            return newUri.ToString();
        }
        return relativePath;
    }
}
