using Bit.Core.Platform.MailDelivery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bit.Core.Test.Platform.MailDelivery;

public class PostConfigureSmtpMailOptionsTests
{
    [Fact]
    public void NormalizeMicrosoft()
    {
        var options = Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:Mail:Smtp:AuthType", "MicrosoftOAuth" },
            { "GlobalSettings:Mail:Smtp:TenantId", "test_tenant_id" },
            { "GlobalSettings:Mail:Smtp:ClientId", "test_client_id" },
            { "GlobalSettings:Mail:Smtp:ClientSecret", "test_client_secret" },
        });

        Assert.Equal("https://login.microsoftonline.com/test_tenant_id/oauth2/v2.0/token", options.OAuth.TokenEndpoint);
        Assert.Equal("client_credentials", options.OAuth.GrantType);
        Assert.Equal("https://outlook.office365.com/.default", options.OAuth.Scope);
        Assert.Equal("test_client_id", options.OAuth.ClientId);
        Assert.Equal("test_client_secret", options.OAuth.ClientSecret);
    }

    [Fact]
    public void NormalizeMicrosoft_WithCustomOverrides()
    {
        var options = Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:Mail:Smtp:AuthType", "MicrosoftOAuth" },
            { "GlobalSettings:Mail:Smtp:OAuth:TokenEndpoint", "https://example.com/token" },
            // Is ignored if TokenEndpoint is manually set
            { "GlobalSettings:Mail:Smtp:TenantId", "test_tenant_id" },

            { "GlobalSettings:Mail:Smtp:OAuth:ClientId", "test_client_id" },
            { "GlobalSettings:Mail:Smtp:OAuth:ClientSecret", "test_client_secret" },

            // Is ignored if the more fully qualified location is set
            { "GlobalSettings:Mail:Smtp:ClientId", "test_client_id_alt" },
            { "GlobalSettings:Mail:Smtp:ClientSecret", "test_client_secret_alt" },

            // Should respect custom defined scope instead of default
            { "GlobalSettings:Mail:Smtp:OAuth:Scope", "custom_scope" },

            // Should respect custom grant_type
            { "GlobalSettings:Mail:Smtp:OAuth:GrantType", "custom_grant_type" },
        });

        Assert.Equal("https://example.com/token", options.OAuth.TokenEndpoint);
        Assert.Equal("custom_grant_type", options.OAuth.GrantType);
        Assert.Equal("custom_scope", options.OAuth.Scope);
        Assert.Equal("test_client_id", options.OAuth.ClientId);
        Assert.Equal("test_client_secret", options.OAuth.ClientSecret);
    }

    [Fact]
    public void MicrosoftOAuth_FailsIfMissingTenantIdAndNoTokenEndpoint()
    {
        var validationException = Assert.Throws<OptionsValidationException>(() =>
        {
            Build(new Dictionary<string, string?>
            {
                { "GlobalSettings:Mail:Smtp:AuthType", "MicrosoftOAuth" },
            });
        });

        // Make sure error mentions the settings to configure to fix the issue.
        Assert.Contains("GlobalSettings:Mail:Smtp:TenantId", validationException.Message);
        Assert.Contains("GlobalSettings:Mail:Smtp:OAuth:TokenEndpoint", validationException.Message);
    }

    [Fact]
    public void MicrosoftOAuth_FailsIfMissingClientIdAtEitherLocation()
    {
        var validationException = Assert.Throws<OptionsValidationException>(() =>
        {
            Build(new Dictionary<string, string?>
            {
                { "GlobalSettings:Mail:Smtp:AuthType", "MicrosoftOAuth" },
                { "GlobalSettings:Mail:Smtp:TenantId", "some_tenant" },
            });
        });

        // Make sure error mentions the settings to configure to fix the issue.
        Assert.Contains("GlobalSettings:Mail:Smtp:ClientId", validationException.Message);
    }

    [Fact]
    public void MicrosoftOAuth_FailsIfMissingClientSecretAtEitherLocation()
    {
        var validationException = Assert.Throws<OptionsValidationException>(() =>
        {
            Build(new Dictionary<string, string?>
            {
                { "GlobalSettings:Mail:Smtp:AuthType", "MicrosoftOAuth" },
                { "GlobalSettings:Mail:Smtp:TenantId", "some_tenant" },
                { "GlobalSettings:Mail:Smtp:ClientId", "client_id" },
            });
        });

        // Make sure error mentions the settings to configure to fix the issue.
        Assert.Contains("GlobalSettings:Mail:Smtp:ClientSecret", validationException.Message);
    }

    [Fact]
    public void NormalizeGoogle()
    {
        var options = Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:Mail:Smtp:AuthType", "GoogleOAuth" },
            { "GlobalSettings:Mail:Smtp:ServiceAccountEmail", "service_account@example.com" },
            { "GlobalSettings:Mail:Smtp:ServiceAccountPrivateKey", "private_key" },
        });

        Assert.Equal("https://oauth2.googleapis.com/token", options.OAuth.TokenEndpoint);
        Assert.Equal("urn:ietf:params:oauth:grant-type:jwt-bearer", options.OAuth.GrantType);
        Assert.Equal("RS256", options.OAuth.Algorithm);
        Assert.Equal("private_key", options.OAuth.SigningKey);
        Assert.Single(options.OAuth.Claims, (claim) => claim.Key == "iss" && claim.Value == "service_account@example.com");
        Assert.Single(options.OAuth.Claims, (claim) => claim.Key == "scope" && claim.Value == "https://mail.google.com/");
        Assert.Single(options.OAuth.Claims, (claim) => claim.Key == "aud" && claim.Value == "https://oauth2.googleapis.com/token");
    }

    [Fact]
    public void NormalizeGoogle_WithCustomOverrides()
    {
        var options = Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:Mail:Smtp:AuthType", "GoogleOAuth" },
            { "GlobalSettings:Mail:Smtp:ServiceAccountEmail", "service_account@example.com" },
            { "GlobalSettings:Mail:Smtp:ServiceAccountPrivateKey", "private_key_alt" },

            // Overrides
            { "GlobalSettings:Mail:Smtp:OAuth:TokenEndpoint", "https://example.com/token" },
            { "GlobalSettings:Mail:Smtp:OAuth:GrantType", "custom_grant_type" },
            { "GlobalSettings:Mail:Smtp:OAuth:SigningKey", "private_key"},
            { "GlobalSettings:Mail:Smtp:OAuth:Algorithm", "RS512" },
            { "GlobalSettings:Mail:Smtp:OAuth:Claims:iss", "custom@example.com" },
            { "GlobalSettings:Mail:Smtp:OAuth:Claims:scope", "custom_scope" },
            { "GlobalSettings:Mail:Smtp:OAuth:Claims:aud", "custom_audience" },
        });

        Assert.Equal("https://example.com/token", options.OAuth.TokenEndpoint);
        Assert.Equal("custom_grant_type", options.OAuth.GrantType);
        Assert.Equal("RS512", options.OAuth.Algorithm);
        Assert.Equal("private_key", options.OAuth.SigningKey);
        Assert.Single(options.OAuth.Claims, (claim) => claim.Key == "iss" && claim.Value == "custom@example.com");
        Assert.Single(options.OAuth.Claims, (claim) => claim.Key == "scope" && claim.Value == "custom_scope");
        Assert.Single(options.OAuth.Claims, (claim) => claim.Key == "aud" && claim.Value == "custom_audience");
    }

    [Fact]
    public void GoogleOAuth_FailsIfMissingPrivateKey()
    {
        var validationException = Assert.Throws<OptionsValidationException>(() =>
        {
            Build(new Dictionary<string, string?>
            {
                { "GlobalSettings:Mail:Smtp:AuthType", "GoogleOAuth" },
            });
        });

        // Make sure error mentions the settings to configure to fix the issue.
        Assert.Contains("GlobalSettings:Mail:Smtp:ServiceAccountPrivateKey", validationException.Message);
        Assert.Contains("GlobalSettings:Mail:Smtp:OAuth:SigningKey", validationException.Message);
    }

    [Fact]
    public void GoogleOAuth_FailsIfMissingServiceAccountEmail()
    {
        var validationException = Assert.Throws<OptionsValidationException>(() =>
        {
            Build(new Dictionary<string, string?>
            {
                { "GlobalSettings:Mail:Smtp:AuthType", "GoogleOAuth" },
                { "GlobalSettings:Mail:Smtp:ServiceAccountPrivateKey", "private_key" },
            });
        });

        // Make sure error mentions the settings to configure to fix the issue.
        Assert.Contains("GlobalSettings:Mail:Smtp:ServiceAccountEmail", validationException.Message);
        Assert.Contains("GlobalSettings:Mail:Smtp:OAuth:Claims:iss", validationException.Message);
    }

    [Fact]
    public void CustomOAuth_WarnsIfUnknownGrantType()
    {
        Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:Mail:Smtp:AuthType", "CustomOAuth" },
            { "GlobalSettings:Mail:Smtp:OAuth:GrantType", "something" },
        }, out var services);

        var logs = services.GetFakeLogCollector().GetSnapshot();
        Assert.Single(logs,
            l => l.Level == LogLevel.Warning && l.Message.Contains("Grant type 'something' is not one of the supported values")
        );
    }

    [Fact]
    public void CustomOAuth_JwtBearer()
    {
        var options = Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:Mail:Smtp:AuthType", "CustomOAuth" },
            // CustomOAuth locations
            { "GlobalSettings:Mail:Smtp:OAuth:TokenEndpoint", "https://example.com/token" },
            { "GlobalSettings:Mail:Smtp:OAuth:GrantType", "urn:ietf:params:oauth:grant-type:jwt-bearer" },
            { "GlobalSettings:Mail:Smtp:OAuth:SigningKey", "private_key"},
            { "GlobalSettings:Mail:Smtp:OAuth:Algorithm", "RS512" },
            { "GlobalSettings:Mail:Smtp:OAuth:Claims:iss", "custom@example.com" },
            { "GlobalSettings:Mail:Smtp:OAuth:Claims:scope", "custom_scope" },
            { "GlobalSettings:Mail:Smtp:OAuth:Claims:aud", "custom_audience" },
        });

        Assert.Equal("https://example.com/token", options.OAuth.TokenEndpoint);
        Assert.Equal("urn:ietf:params:oauth:grant-type:jwt-bearer", options.OAuth.GrantType);
        Assert.Equal("RS512", options.OAuth.Algorithm);
        Assert.Equal("private_key", options.OAuth.SigningKey);
        Assert.Single(options.OAuth.Claims, (claim) => claim.Key == "iss" && claim.Value == "custom@example.com");
        Assert.Single(options.OAuth.Claims, (claim) => claim.Key == "scope" && claim.Value == "custom_scope");
        Assert.Single(options.OAuth.Claims, (claim) => claim.Key == "aud" && claim.Value == "custom_audience");

        // TODO: Assert no bad grant_type warning
    }

    [Fact]
    public void CustomOAuth_ClientCredentials()
    {
        var options = Build(new Dictionary<string, string?>
        {
            { "GlobalSettings:Mail:Smtp:AuthType", "CustomOAuth" },
            { "GlobalSettings:Mail:Smtp:OAuth:TokenEndpoint", "https://example.com/token" },
            { "GlobalSettings:Mail:Smtp:OAuth:ClientId", "test_client_id" },
            { "GlobalSettings:Mail:Smtp:OAuth:ClientSecret", "test_client_secret" },

            // Should respect custom defined scope instead of default
            { "GlobalSettings:Mail:Smtp:OAuth:Scope", "custom_scope" },

            // Should respect custom grant_type
            { "GlobalSettings:Mail:Smtp:OAuth:GrantType", "client_credentials" },
        });

        Assert.Equal("https://example.com/token", options.OAuth.TokenEndpoint);
        Assert.Equal("client_credentials", options.OAuth.GrantType);
        Assert.Equal("custom_scope", options.OAuth.Scope);
        Assert.Equal("test_client_id", options.OAuth.ClientId);
        Assert.Equal("test_client_secret", options.OAuth.ClientSecret);

        // TODO: Assert no bad grant_type warning
    }


    private SmtpMailOptions Build(Dictionary<string, string?> initialData) => Build(initialData, out _);
    private SmtpMailOptions Build(Dictionary<string, string?> initialData, out IServiceProvider serviceProvider)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(initialData)
            .Build()
        );
        services.AddFakeLogging();

        services.AddMailDelivery();

        serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IOptions<SmtpMailOptions>>().Value;
    }
}
