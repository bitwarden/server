using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Bit.Core.Services;
using Bit.Core;
using Bit.Core.Models.Data;
using Bit.Core.Enums;
using Bit.Core.Sso;

namespace Bit.Portal.Models
{
    public class SsoConfigDataViewModel : IValidatableObject
    {
        public SsoConfigDataViewModel() { }

        public SsoConfigDataViewModel(SsoConfigurationData configurationData, GlobalSettings globalSettings)
        {
            ConfigType = configurationData.ConfigType;
            Authority = configurationData.Authority;
            ClientId = configurationData.ClientId;
            ClientSecret = configurationData.ClientSecret;
            CallbackPath = configurationData.BuildCallbackPath(globalSettings.BaseServiceUri.Sso);
            SignedOutCallbackPath = configurationData.BuildSignedOutCallbackPath(globalSettings.BaseServiceUri.Sso);
            MetadataAddress = configurationData.MetadataAddress;
            GetClaimsFromUserInfoEndpoint = configurationData.GetClaimsFromUserInfoEndpoint;
            SpEntityId = configurationData.BuildSaml2ModulePath(globalSettings.BaseServiceUri.Sso);
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
        }

        [Required]
        [Display(Name = "ConfigType")]
        public SsoType ConfigType { get; set; }

        // OIDC
        [Display(Name = "Authority")]
        public string Authority { get; set; }
        [Display(Name = "ClientId")]
        public string ClientId { get; set; }
        [Display(Name = "ClientSecret")]
        public string ClientSecret { get; set; }
        [Display(Name = "CallbackPath")]
        public string CallbackPath { get; set; }
        [Display(Name = "SignedOutCallbackPath")]
        public string SignedOutCallbackPath { get; set; }
        [Display(Name = "MetadataAddress")]
        public string MetadataAddress { get; set; }
        [Display(Name = "GetClaimsFromUserInfoEndpoint")]
        public bool GetClaimsFromUserInfoEndpoint { get; set; }

        // SAML2 SP
        [Display(Name = "SpEntityId")]
        public string SpEntityId { get; set; }
        [Display(Name = "NameIdFormat")]
        public Saml2NameIdFormat SpNameIdFormat { get; set; }
        [Display(Name = "OutboundSigningAlgorithm")]
        public string SpOutboundSigningAlgorithm { get; set; }
        [Display(Name = "SigningBehavior")]
        public Saml2SigningBehavior SpSigningBehavior { get; set; }
        [Display(Name = "SpWantAssertionsSigned")]
        public bool SpWantAssertionsSigned { get; set; }
        [Display(Name = "SpValidateCertificates")]
        public bool SpValidateCertificates { get; set; }

        // SAML2 IDP
        [Display(Name = "EntityId")]
        public string IdpEntityId { get; set; }
        [Display(Name = "BindingType")]
        public Saml2BindingType IdpBindingType { get; set; }
        [Display(Name = "SingleSignOnServiceUrl")]
        public string IdpSingleSignOnServiceUrl { get; set; }
        [Display(Name = "SingleLogoutServiceUrl")]
        public string IdpSingleLogoutServiceUrl { get; set; }
        [Display(Name = "ArtifactResolutionServiceUrl")]
        public string IdpArtifactResolutionServiceUrl { get; set; }
        [Display(Name = "X509PublicCert")]
        public string IdpX509PublicCert { get; set; }
        [Display(Name = "OutboundSigningAlgorithm")]
        public string IdpOutboundSigningAlgorithm { get; set; }
        [Display(Name = "AllowUnsolicitedAuthnResponse")]
        public bool IdpAllowUnsolicitedAuthnResponse { get; set; }
        [Display(Name = "DisableOutboundLogoutRequests")]
        public bool IdpDisableOutboundLogoutRequests { get; set; }
        [Display(Name = "WantAuthnRequestsSigned")]
        public bool IdpWantAuthnRequestsSigned { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            var i18nService = context.GetService(typeof(II18nService)) as I18nService;
            var model = context.ObjectInstance as SsoConfigDataViewModel;

            if (model.ConfigType == SsoType.OpenIdConnect)
            {
                if (string.IsNullOrWhiteSpace(model.Authority))
                {
                    yield return new ValidationResult(i18nService.GetLocalizedHtmlString("AuthorityValidationError"),
                        new[] { nameof(model.Authority) });
                }

                if (string.IsNullOrWhiteSpace(model.ClientId))
                {
                    yield return new ValidationResult(i18nService.GetLocalizedHtmlString("ClientIdValidationError"),
                        new[] { nameof(model.ClientId) });
                }

                if (string.IsNullOrWhiteSpace(model.ClientSecret))
                {
                    yield return new ValidationResult(i18nService.GetLocalizedHtmlString("ClientSecretValidationError"),
                        new[] { nameof(model.ClientSecret) });
                }
            }
            else if (model.ConfigType == SsoType.Saml2)
            {
                if (string.IsNullOrWhiteSpace(model.IdpEntityId))
                {
                    yield return new ValidationResult(i18nService.GetLocalizedHtmlString("IdpEntityIdValidationError"),
                        new[] { nameof(model.IdpEntityId) });
                }

                if (model.IdpBindingType == Saml2BindingType.Artifact && string.IsNullOrWhiteSpace(model.IdpArtifactResolutionServiceUrl))
                {
                    yield return new ValidationResult(i18nService.GetLocalizedHtmlString("Saml2BindingTypeValidationError"),
                        new[] { nameof(model.IdpArtifactResolutionServiceUrl) });
                }

                if (!Uri.IsWellFormedUriString(model.IdpEntityId, UriKind.Absolute) && string.IsNullOrWhiteSpace(model.IdpSingleSignOnServiceUrl))
                {
                    yield return new ValidationResult(i18nService.GetLocalizedHtmlString("IdpSingleSignOnServiceUrlValidationError"),
                        new[] { nameof(model.IdpSingleSignOnServiceUrl) });
                }
            }
        }

        public SsoConfigurationData ToConfigurationData()
        {
            return new SsoConfigurationData
            {
                ConfigType = ConfigType,
                Authority = Authority,
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                MetadataAddress = MetadataAddress,
                GetClaimsFromUserInfoEndpoint = GetClaimsFromUserInfoEndpoint,
                IdpEntityId = IdpEntityId,
                IdpBindingType = IdpBindingType,
                IdpSingleSignOnServiceUrl = IdpSingleSignOnServiceUrl,
                IdpSingleLogoutServiceUrl = IdpSingleLogoutServiceUrl,
                IdpArtifactResolutionServiceUrl = IdpArtifactResolutionServiceUrl,
                IdpX509PublicCert = IdpX509PublicCert,
                IdpOutboundSigningAlgorithm = IdpOutboundSigningAlgorithm,
                IdpAllowUnsolicitedAuthnResponse = IdpAllowUnsolicitedAuthnResponse,
                IdpDisableOutboundLogoutRequests = IdpDisableOutboundLogoutRequests,
                IdpWantAuthnRequestsSigned = IdpWantAuthnRequestsSigned,
                SpNameIdFormat = SpNameIdFormat,
                SpOutboundSigningAlgorithm = SpOutboundSigningAlgorithm ?? SamlSigningAlgorithms.Sha256,
                SpSigningBehavior = SpSigningBehavior,
                SpWantAssertionsSigned = SpWantAssertionsSigned,
                SpValidateCertificates = SpValidateCertificates,
            };
        }
    }
}
