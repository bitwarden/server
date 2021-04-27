using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Sso;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Bit.Core.Models.Data
{
    public class SsoConfigurationData
    {
        private const string _oidcSigninPath = "/oidc-signin";
        private const string _oidcSignedOutPath = "/oidc-signedout";
        private const string _saml2ModulePath = "/saml2";

        public SsoType ConfigType { get; set; }

        // OIDC
        public string Authority { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string MetadataAddress { get; set; }
        public OpenIdConnectRedirectBehavior RedirectBehavior { get; set; } = OpenIdConnectRedirectBehavior.FormPost;
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
        public string IdpArtifactResolutionServiceUrl { get; set; }
        public bool IdpDisableOutboundLogoutRequests { get; set; }
        public string IdpOutboundSigningAlgorithm { get; set; }
        public bool IdpWantAuthnRequestsSigned { get; set; }

        // SAML2 SP
        public Saml2NameIdFormat SpNameIdFormat { get; set; } = Saml2NameIdFormat.Persistent;
        public string SpOutboundSigningAlgorithm { get; set; } = SamlSigningAlgorithms.Sha256;
        public Saml2SigningBehavior SpSigningBehavior { get; set; } = Saml2SigningBehavior.IfIdpWantAuthnRequestsSigned;
        public bool SpWantAssertionsSigned { get; set; }
        public bool SpValidateCertificates { get; set; }
        public string SpMinIncomingSigningAlgorithm { get; set; } = SamlSigningAlgorithms.Sha256;

        public string BuildCallbackPath(string ssoUri = null)
        {
            return BuildSsoUrl(_oidcSigninPath, ssoUri);
        }

        public string BuildSignedOutCallbackPath(string ssoUri = null)
        {
            return BuildSsoUrl(_oidcSignedOutPath, ssoUri);
        }

        public string BuildSaml2ModulePath(string ssoUri = null, string scheme = null)
        {
            return string.Concat(BuildSsoUrl(_saml2ModulePath, ssoUri),
                string.IsNullOrWhiteSpace(scheme) ? string.Empty : $"/{scheme}");
        }

        public string BuildSaml2AcsUrl(string ssoUri = null, string scheme = null)
        {
            return string.Concat(BuildSaml2ModulePath(ssoUri, scheme), "/Acs");
        }

        public string BuildSaml2MetadataUrl(string ssoUri = null, string scheme = null)
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

        private string BuildSsoUrl(string relativePath, string ssoUri)
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
}
