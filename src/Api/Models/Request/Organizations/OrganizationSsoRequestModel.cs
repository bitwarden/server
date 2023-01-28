using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Core.Sso;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationSsoRequestModel
{
    [Required]
    public bool Enabled { get; set; }
    [StringLength(50)]
    public string Identifier { get; set; }
    [Required]
    public SsoConfigurationDataRequest Data { get; set; }

    public SsoConfig ToSsoConfig(Guid organizationId)
    {
        return ToSsoConfig(new SsoConfig { OrganizationId = organizationId });
    }

    public SsoConfig ToSsoConfig(SsoConfig existingConfig)
    {
        existingConfig.Enabled = Enabled;
        var configurationData = Data.ToConfigurationData();
        existingConfig.SetData(configurationData);
        return existingConfig;
    }
}

public class SsoConfigurationDataRequest : IValidatableObject
{
    public SsoConfigurationDataRequest() { }

    [Required]
    public SsoType ConfigType { get; set; }

    public bool KeyConnectorEnabled { get; set; }
    public string KeyConnectorUrl { get; set; }

    // OIDC
    public string Authority { get; set; }
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string MetadataAddress { get; set; }
    public OpenIdConnectRedirectBehavior RedirectBehavior { get; set; }
    public bool? GetClaimsFromUserInfoEndpoint { get; set; }
    public string AdditionalScopes { get; set; }
    public string AdditionalUserIdClaimTypes { get; set; }
    public string AdditionalEmailClaimTypes { get; set; }
    public string AdditionalNameClaimTypes { get; set; }
    public string AcrValues { get; set; }
    public string ExpectedReturnAcrValue { get; set; }

    // SAML2 SP
    public Saml2NameIdFormat SpNameIdFormat { get; set; }
    public string SpOutboundSigningAlgorithm { get; set; }
    public Saml2SigningBehavior SpSigningBehavior { get; set; }
    public bool? SpWantAssertionsSigned { get; set; }
    public bool? SpValidateCertificates { get; set; }
    public string SpMinIncomingSigningAlgorithm { get; set; }

    // SAML2 IDP
    public string IdpEntityId { get; set; }
    public Saml2BindingType IdpBindingType { get; set; }
    public string IdpSingleSignOnServiceUrl { get; set; }
    public string IdpSingleLogoutServiceUrl { get; set; }
    public string IdpArtifactResolutionServiceUrl { get => null; set { /*IGNORE*/ } }
    public string IdpX509PublicCert { get; set; }
    public string IdpOutboundSigningAlgorithm { get; set; }
    public bool? IdpAllowUnsolicitedAuthnResponse { get; set; }
    public bool? IdpDisableOutboundLogoutRequests { get; set; }
    public bool? IdpWantAuthnRequestsSigned { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        var i18nService = context.GetService(typeof(II18nService)) as I18nService;

        if (ConfigType == SsoType.OpenIdConnect)
        {
            if (string.IsNullOrWhiteSpace(Authority))
            {
                yield return new ValidationResult(i18nService.GetLocalizedHtmlString("AuthorityValidationError"),
                    new[] { nameof(Authority) });
            }

            if (string.IsNullOrWhiteSpace(ClientId))
            {
                yield return new ValidationResult(i18nService.GetLocalizedHtmlString("ClientIdValidationError"),
                    new[] { nameof(ClientId) });
            }

            if (string.IsNullOrWhiteSpace(ClientSecret))
            {
                yield return new ValidationResult(i18nService.GetLocalizedHtmlString("ClientSecretValidationError"),
                    new[] { nameof(ClientSecret) });
            }
        }
        else if (ConfigType == SsoType.Saml2)
        {
            if (string.IsNullOrWhiteSpace(IdpEntityId))
            {
                yield return new ValidationResult(i18nService.GetLocalizedHtmlString("IdpEntityIdValidationError"),
                    new[] { nameof(IdpEntityId) });
            }

            if (!Uri.IsWellFormedUriString(IdpEntityId, UriKind.Absolute) && string.IsNullOrWhiteSpace(IdpSingleSignOnServiceUrl))
            {
                yield return new ValidationResult(i18nService.GetLocalizedHtmlString("IdpSingleSignOnServiceUrlValidationError"),
                    new[] { nameof(IdpSingleSignOnServiceUrl) });
            }

            if (InvalidServiceUrl(IdpSingleSignOnServiceUrl))
            {
                yield return new ValidationResult(i18nService.GetLocalizedHtmlString("IdpSingleSignOnServiceUrlInvalid"),
                    new[] { nameof(IdpSingleSignOnServiceUrl) });
            }

            if (InvalidServiceUrl(IdpSingleLogoutServiceUrl))
            {
                yield return new ValidationResult(i18nService.GetLocalizedHtmlString("IdpSingleLogoutServiceUrlInvalid"),
                    new[] { nameof(IdpSingleLogoutServiceUrl) });
            }

            if (!string.IsNullOrWhiteSpace(IdpX509PublicCert))
            {
                // Validate the certificate is in a valid format
                ValidationResult failedResult = null;
                try
                {
                    var certData = CoreHelpers.Base64UrlDecode(StripPemCertificateElements(IdpX509PublicCert));
                    new X509Certificate2(certData);
                }
                catch (FormatException)
                {
                    failedResult = new ValidationResult(i18nService.GetLocalizedHtmlString("IdpX509PublicCertInvalidFormatValidationError"),
                        new[] { nameof(IdpX509PublicCert) });
                }
                catch (CryptographicException cryptoEx)
                {
                    failedResult = new ValidationResult(i18nService.GetLocalizedHtmlString("IdpX509PublicCertCryptographicExceptionValidationError", cryptoEx.Message),
                        new[] { nameof(IdpX509PublicCert) });
                }
                catch (Exception ex)
                {
                    failedResult = new ValidationResult(i18nService.GetLocalizedHtmlString("IdpX509PublicCertValidationError", ex.Message),
                        new[] { nameof(IdpX509PublicCert) });
                }
                if (failedResult != null)
                {
                    yield return failedResult;
                }
            }
        }
    }

    public SsoConfigurationData ToConfigurationData()
    {
        return new SsoConfigurationData
        {
            ConfigType = ConfigType,
            KeyConnectorEnabled = KeyConnectorEnabled,
            KeyConnectorUrl = KeyConnectorUrl,
            Authority = Authority,
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            MetadataAddress = MetadataAddress,
            GetClaimsFromUserInfoEndpoint = GetClaimsFromUserInfoEndpoint.GetValueOrDefault(),
            RedirectBehavior = RedirectBehavior,
            IdpEntityId = IdpEntityId,
            IdpBindingType = IdpBindingType,
            IdpSingleSignOnServiceUrl = IdpSingleSignOnServiceUrl,
            IdpSingleLogoutServiceUrl = IdpSingleLogoutServiceUrl,
            IdpArtifactResolutionServiceUrl = null,
            IdpX509PublicCert = StripPemCertificateElements(IdpX509PublicCert),
            IdpOutboundSigningAlgorithm = IdpOutboundSigningAlgorithm,
            IdpAllowUnsolicitedAuthnResponse = IdpAllowUnsolicitedAuthnResponse.GetValueOrDefault(),
            IdpDisableOutboundLogoutRequests = IdpDisableOutboundLogoutRequests.GetValueOrDefault(),
            IdpWantAuthnRequestsSigned = IdpWantAuthnRequestsSigned.GetValueOrDefault(),
            SpNameIdFormat = SpNameIdFormat,
            SpOutboundSigningAlgorithm = SpOutboundSigningAlgorithm ?? SamlSigningAlgorithms.Sha256,
            SpSigningBehavior = SpSigningBehavior,
            SpWantAssertionsSigned = SpWantAssertionsSigned.GetValueOrDefault(),
            SpValidateCertificates = SpValidateCertificates.GetValueOrDefault(),
            SpMinIncomingSigningAlgorithm = SpMinIncomingSigningAlgorithm,
            AdditionalScopes = AdditionalScopes,
            AdditionalUserIdClaimTypes = AdditionalUserIdClaimTypes,
            AdditionalEmailClaimTypes = AdditionalEmailClaimTypes,
            AdditionalNameClaimTypes = AdditionalNameClaimTypes,
            AcrValues = AcrValues,
            ExpectedReturnAcrValue = ExpectedReturnAcrValue,
        };
    }

    private string StripPemCertificateElements(string certificateText)
    {
        if (string.IsNullOrWhiteSpace(certificateText))
        {
            return null;
        }
        return Regex.Replace(certificateText,
            @"(((BEGIN|END) CERTIFICATE)|([\-\n\r\t\s\f]))",
            string.Empty,
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private bool InvalidServiceUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            return true;
        }
        return Regex.IsMatch(url, "[<>\"]");
    }
}
