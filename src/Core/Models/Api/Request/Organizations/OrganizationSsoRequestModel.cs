using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Bit.Core.Services;
using Bit.Core.Models.Data;
using Bit.Core.Enums;
using Bit.Core.Sso;
using U2F.Core.Utils;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bit.Core.Models.Table;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Bit.Core.Models.Api
{
    public class OrganizationSsoRequestModel
    {
        [Required]
        public bool Enabled { get; set; }
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
        public SsoConfigurationDataRequest() {}

        public SsoConfigurationDataRequest(SsoConfigurationData configurationData)
        {
            ConfigType = configurationData.ConfigType;
            UseCryptoAgent = configurationData.UseCryptoAgent;
            CryptoAgentUrl = configurationData.CryptoAgentUrl;
            Authority = configurationData.Authority;
            ClientId = configurationData.ClientId;
            ClientSecret = configurationData.ClientSecret;
            MetadataAddress = configurationData.MetadataAddress;
            RedirectBehavior = configurationData.RedirectBehavior;
            GetClaimsFromUserInfoEndpoint = configurationData.GetClaimsFromUserInfoEndpoint;
            IdpEntityId = configurationData.IdpEntityId;
            IdpBindingType = configurationData.IdpBindingType;
            IdpSingleSignOnServiceUrl = configurationData.IdpSingleSignOnServiceUrl;
            IdpSingleLogoutServiceUrl = configurationData.IdpSingleLogoutServiceUrl;
            IdpArtifactResolutionServiceUrl = configurationData.IdpArtifactResolutionServiceUrl;
            IdpX509PublicCert = configurationData.IdpX509PublicCert;
            IdpOutboundSigningAlgorithm = configurationData.IdpOutboundSigningAlgorithm;
            IdpAllowUnsolicitedAuthnResponse = configurationData.IdpAllowUnsolicitedAuthnResponse;
            IdpDisableOutboundLogoutRequests = configurationData.IdpDisableOutboundLogoutRequests;
            IdpWantAuthnRequestsSigned = configurationData.IdpWantAuthnRequestsSigned;
            SpNameIdFormat = configurationData.SpNameIdFormat;
            SpOutboundSigningAlgorithm = configurationData.SpOutboundSigningAlgorithm ?? SamlSigningAlgorithms.Sha256;
            SpSigningBehavior = configurationData.SpSigningBehavior;
            SpWantAssertionsSigned = configurationData.SpWantAssertionsSigned;
            SpValidateCertificates = configurationData.SpValidateCertificates;
            SpMinIncomingSigningAlgorithm = configurationData.SpMinIncomingSigningAlgorithm ?? SamlSigningAlgorithms.Sha256;
            AdditionalScopes = configurationData.AdditionalScopes;
            AdditionalUserIdClaimTypes = configurationData.AdditionalUserIdClaimTypes;
            AdditionalEmailClaimTypes = configurationData.AdditionalEmailClaimTypes;
            AdditionalNameClaimTypes = configurationData.AdditionalNameClaimTypes;
            AcrValues = configurationData.AcrValues;
            ExpectedReturnAcrValue = configurationData.ExpectedReturnAcrValue;
        }

        [Required]
        public SsoType ConfigType { get; set; }

        // Crypto Agent
        public bool UseCryptoAgent { get; set; }
        public string CryptoAgentUrl { get; set; }

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

        // SAML2 SP
        public Saml2NameIdFormat SpNameIdFormat { get; set; }
        public string SpOutboundSigningAlgorithm { get; set; }
        public Saml2SigningBehavior SpSigningBehavior { get; set; }
        public bool SpWantAssertionsSigned { get; set; }
        public bool SpValidateCertificates { get; set; }
        public string SpMinIncomingSigningAlgorithm { get; set; }

        // SAML2 IDP
        public string IdpEntityId { get; set; }
        public Saml2BindingType IdpBindingType { get; set; }
        public string IdpSingleSignOnServiceUrl { get; set; }
        public string IdpSingleLogoutServiceUrl { get; set; }
        public string IdpArtifactResolutionServiceUrl { get; set; }
        public string IdpX509PublicCert { get; set; }
        public string IdpOutboundSigningAlgorithm { get; set; }
        public bool IdpAllowUnsolicitedAuthnResponse { get; set; }
        public bool IdpDisableOutboundLogoutRequests { get; set; }
        public bool IdpWantAuthnRequestsSigned { get; set; }

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

                if (IdpBindingType == Saml2BindingType.Artifact && string.IsNullOrWhiteSpace(IdpArtifactResolutionServiceUrl))
                {
                    yield return new ValidationResult(i18nService.GetLocalizedHtmlString("Saml2BindingTypeValidationError"),
                        new[] { nameof(IdpArtifactResolutionServiceUrl) });
                }

                if (!Uri.IsWellFormedUriString(IdpEntityId, UriKind.Absolute) && string.IsNullOrWhiteSpace(IdpSingleSignOnServiceUrl))
                {
                    yield return new ValidationResult(i18nService.GetLocalizedHtmlString("IdpSingleSignOnServiceUrlValidationError"),
                        new[] { nameof(IdpSingleSignOnServiceUrl) });
                }
                if (!string.IsNullOrWhiteSpace(IdpX509PublicCert))
                {
                    // Validate the certificate is in a valid format
                    ValidationResult failedResult = null;
                    try
                    {
                        var certData = StripPemCertificateElements(IdpX509PublicCert).Base64StringToByteArray();
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
                UseCryptoAgent = UseCryptoAgent,
                CryptoAgentUrl = CryptoAgentUrl,
                Authority = Authority,
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                MetadataAddress = MetadataAddress,
                GetClaimsFromUserInfoEndpoint = GetClaimsFromUserInfoEndpoint,
                RedirectBehavior = RedirectBehavior,
                IdpEntityId = IdpEntityId,
                IdpBindingType = IdpBindingType,
                IdpSingleSignOnServiceUrl = IdpSingleSignOnServiceUrl,
                IdpSingleLogoutServiceUrl = IdpSingleLogoutServiceUrl,
                IdpArtifactResolutionServiceUrl = IdpArtifactResolutionServiceUrl,
                IdpX509PublicCert = StripPemCertificateElements(IdpX509PublicCert),
                IdpOutboundSigningAlgorithm = IdpOutboundSigningAlgorithm,
                IdpAllowUnsolicitedAuthnResponse = IdpAllowUnsolicitedAuthnResponse,
                IdpDisableOutboundLogoutRequests = IdpDisableOutboundLogoutRequests,
                IdpWantAuthnRequestsSigned = IdpWantAuthnRequestsSigned,
                SpNameIdFormat = SpNameIdFormat,
                SpOutboundSigningAlgorithm = SpOutboundSigningAlgorithm ?? SamlSigningAlgorithms.Sha256,
                SpSigningBehavior = SpSigningBehavior,
                SpWantAssertionsSigned = SpWantAssertionsSigned,
                SpValidateCertificates = SpValidateCertificates,
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
    }
}
