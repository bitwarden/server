using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.Organizations;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Services;
using Bit.Core.Sso;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request;

public class OrganizationSsoRequestModelTests
{
    [Fact]
    public void ToSsoConfig_WithOrganizationId_CreatesNewSsoConfig()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var model = new OrganizationSsoRequestModel
        {
            Enabled = true,
            Identifier = "test-identifier",
            Data = new SsoConfigurationDataRequest
            {
                ConfigType = SsoType.OpenIdConnect,
                Authority = "https://example.com",
                ClientId = "test-client",
                ClientSecret = "test-secret"
            }
        };

        // Act
        var result = model.ToSsoConfig(organizationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(organizationId, result.OrganizationId);
        Assert.True(result.Enabled);
    }

    [Fact]
    public void ToSsoConfig_WithExistingConfig_UpdatesExistingConfig()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var existingConfig = new SsoConfig
        {
            Id = 1,
            OrganizationId = organizationId,
            Enabled = false
        };

        var model = new OrganizationSsoRequestModel
        {
            Enabled = true,
            Identifier = "updated-identifier",
            Data = new SsoConfigurationDataRequest
            {
                ConfigType = SsoType.Saml2,
                IdpEntityId = "test-entity",
                IdpSingleSignOnServiceUrl = "https://sso.example.com"
            }
        };

        // Act
        var result = model.ToSsoConfig(existingConfig);

        // Assert
        Assert.Same(existingConfig, result);
        Assert.Equal(organizationId, result.OrganizationId);
        Assert.True(result.Enabled);
    }
}

public class SsoConfigurationDataRequestTests
{
    private readonly TestI18nService _i18nService;
    private readonly ValidationContext _validationContext;

    public SsoConfigurationDataRequestTests()
    {
        _i18nService = new TestI18nService();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(II18nService)).Returns(_i18nService);
        _validationContext = new ValidationContext(new object(), serviceProvider, null);
    }

    [Fact]
    public void ToConfigurationData_MapsProperties()
    {
        // Arrange
        var model = new SsoConfigurationDataRequest
        {
            ConfigType = SsoType.OpenIdConnect,
            MemberDecryptionType = MemberDecryptionType.KeyConnector,
            Authority = "https://authority.example.com",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            IdpX509PublicCert = "-----BEGIN CERTIFICATE-----\nMIIC...test\n-----END CERTIFICATE-----",
            SpOutboundSigningAlgorithm = null // Test default
        };

        // Act
        var result = model.ToConfigurationData();

        // Assert
        Assert.Equal(SsoType.OpenIdConnect, result.ConfigType);
        Assert.Equal(MemberDecryptionType.KeyConnector, result.MemberDecryptionType);
        Assert.Equal("https://authority.example.com", result.Authority);
        Assert.Equal("test-client-id", result.ClientId);
        Assert.Equal("test-client-secret", result.ClientSecret);
        Assert.Equal("MIIC...test", result.IdpX509PublicCert); // PEM headers stripped
        Assert.Equal(SamlSigningAlgorithms.Sha256, result.SpOutboundSigningAlgorithm); // Default applied
        Assert.Null(result.IdpArtifactResolutionServiceUrl); // Always null
    }

    [Fact]
    public void KeyConnectorEnabled_Setter_UpdatesMemberDecryptionType()
    {
        // Arrange
        var model = new SsoConfigurationDataRequest();

        // Act & Assert
#pragma warning disable CS0618 // Type or member is obsolete
        model.KeyConnectorEnabled = true;
        Assert.Equal(MemberDecryptionType.KeyConnector, model.MemberDecryptionType);

        model.KeyConnectorEnabled = false;
        Assert.Equal(MemberDecryptionType.MasterPassword, model.MemberDecryptionType);
#pragma warning restore CS0618 // Type or member is obsolete
    }

    // Validation Tests
    [Fact]
    public void Validate_OpenIdConnect_ValidData_NoErrors()
    {
        // Arrange
        var model = new SsoConfigurationDataRequest
        {
            ConfigType = SsoType.OpenIdConnect,
            Authority = "https://example.com",
            ClientId = "test-client",
            ClientSecret = "test-secret"
        };

        // Act
        var results = model.Validate(_validationContext).ToList();

        // Assert
        Assert.Empty(results);
    }

    [Theory]
    [InlineData("", "test-client", "test-secret", "AuthorityValidationError")]
    [InlineData("https://example.com", "", "test-secret", "ClientIdValidationError")]
    [InlineData("https://example.com", "test-client", "", "ClientSecretValidationError")]
    public void Validate_OpenIdConnect_MissingRequiredFields_ReturnsErrors(string authority, string clientId, string clientSecret, string expectedError)
    {
        // Arrange
        var model = new SsoConfigurationDataRequest
        {
            ConfigType = SsoType.OpenIdConnect,
            Authority = authority,
            ClientId = clientId,
            ClientSecret = clientSecret
        };

        // Act
        var results = model.Validate(_validationContext).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal(expectedError, results[0].ErrorMessage);
    }

    [Fact]
    public void Validate_Saml2_ValidData_NoErrors()
    {
        // Arrange
        var model = new SsoConfigurationDataRequest
        {
            ConfigType = SsoType.Saml2,
            IdpEntityId = "https://idp.example.com",
            IdpSingleSignOnServiceUrl = "https://sso.example.com",
            IdpSingleLogoutServiceUrl = "https://logout.example.com"
        };

        // Act
        var results = model.Validate(_validationContext).ToList();

        // Assert
        Assert.Empty(results);
    }

    [Theory]
    [InlineData("", "https://sso.example.com", "IdpEntityIdValidationError")]
    [InlineData("not-a-valid-uri", "", "IdpSingleSignOnServiceUrlValidationError")]
    public void Validate_Saml2_MissingRequiredFields_ReturnsErrors(string entityId, string signOnUrl, string expectedError)
    {
        // Arrange
        var model = new SsoConfigurationDataRequest
        {
            ConfigType = SsoType.Saml2,
            IdpEntityId = entityId,
            IdpSingleSignOnServiceUrl = signOnUrl
        };

        // Act
        var results = model.Validate(_validationContext).ToList();

        // Assert
        Assert.Contains(results, r => r.ErrorMessage == expectedError);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("https://example.com<script>")]
    [InlineData("https://example.com\"onclick")]
    public void Validate_Saml2_InvalidUrls_ReturnsErrors(string invalidUrl)
    {
        // Arrange
        var model = new SsoConfigurationDataRequest
        {
            ConfigType = SsoType.Saml2,
            IdpEntityId = "https://idp.example.com",
            IdpSingleSignOnServiceUrl = invalidUrl,
            IdpSingleLogoutServiceUrl = invalidUrl
        };

        // Act
        var results = model.Validate(_validationContext).ToList();

        // Assert
        Assert.Contains(results, r => r.ErrorMessage == "IdpSingleSignOnServiceUrlInvalid");
        Assert.Contains(results, r => r.ErrorMessage == "IdpSingleLogoutServiceUrlInvalid");
    }

    [Fact]
    public void Validate_Saml2_MissingSignOnUrl_AlwaysReturnsError()
    {
        // Arrange - SignOnUrl is always required for SAML2, regardless of EntityId format
        var model = new SsoConfigurationDataRequest
        {
            ConfigType = SsoType.Saml2,
            IdpEntityId = "https://idp.example.com", // Valid URI
            IdpSingleSignOnServiceUrl = "" // Missing - always causes error
        };

        // Act
        var results = model.Validate(_validationContext).ToList();

        // Assert - Should always fail validation when SignOnUrl is missing
        Assert.Contains(results, r => r.ErrorMessage == "IdpSingleSignOnServiceUrlValidationError");
    }

    [Fact]
    public void Validate_Saml2_InvalidCertificate_ReturnsError()
    {
        // Arrange
        var model = new SsoConfigurationDataRequest
        {
            ConfigType = SsoType.Saml2,
            IdpEntityId = "https://idp.example.com",
            IdpSingleSignOnServiceUrl = "https://sso.example.com",
            IdpX509PublicCert = "invalid-certificate-data"
        };

        // Act
        var results = model.Validate(_validationContext).ToList();

        // Assert
        Assert.Contains(results, r => r.ErrorMessage.Contains("IdpX509PublicCert") && r.ErrorMessage.Contains("ValidationError"));
    }

    [Fact]
    public void Validate_Saml2_EmptyCertificate_PassesValidation()
    {
        // Arrange
        var model = new SsoConfigurationDataRequest
        {
            ConfigType = SsoType.Saml2,
            IdpEntityId = "https://idp.example.com",
            IdpSingleSignOnServiceUrl = "https://sso.example.com",
            IdpX509PublicCert = ""
        };

        // Act
        var results = model.Validate(_validationContext).ToList();

        // Assert
        Assert.DoesNotContain(results, r => r.MemberNames.Contains("IdpX509PublicCert"));
    }

    private class TestI18nService : I18nService
    {
        public TestI18nService() : base(CreateMockLocalizerFactory()) { }

        private static IStringLocalizerFactory CreateMockLocalizerFactory()
        {
            var factory = Substitute.For<IStringLocalizerFactory>();
            var localizer = Substitute.For<IStringLocalizer>();

            localizer[Arg.Any<string>()].Returns(callInfo => new LocalizedString(callInfo.Arg<string>(), callInfo.Arg<string>()));
            localizer[Arg.Any<string>(), Arg.Any<object[]>()].Returns(callInfo => new LocalizedString(callInfo.Arg<string>(), callInfo.Arg<string>()));

            factory.Create(Arg.Any<string>(), Arg.Any<string>()).Returns(localizer);
            return factory;
        }
    }
}
