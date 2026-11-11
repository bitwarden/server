using Bit.Core.Platform.Mail.Delivery;

namespace Bit.MailDelivery.Test.SmtpOAuth;

public class SmtpOAuthOptionsSetupTests
{
    private readonly SmtpOAuthOptionsSetup _sut = new();

    [Fact]
    public void PostConfigure_PasswordAuth_LeavesOptionsUnchanged()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.Password;

        _sut.PostConfigure(name: null, options);

        Assert.Null(options.Smtp.OAuth.TokenEndpoint);
        Assert.Null(options.Smtp.OAuth.GrantType);
        Assert.Null(options.Smtp.OAuth.Scope);
        Assert.Null(options.Smtp.OAuth.ClientId);
        Assert.Null(options.Smtp.OAuth.ClientSecret);
    }

    [Fact]
    public void Validate_PasswordAuth_ReturnsSuccess()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.Password;

        var result = _sut.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void PostConfigure_MicrosoftOAuthWithTenantId_DerivesTokenEndpoint()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.MicrosoftOAuth;
        options.Smtp.TenantId = "test-tenant";

        _sut.PostConfigure(name: null, options);

        Assert.Equal(
            "https://login.microsoftonline.com/test-tenant/oauth2/v2.0/token",
            options.Smtp.OAuth.TokenEndpoint);
    }

    [Fact]
    public void PostConfigure_MicrosoftOAuthWithExplicitTokenEndpoint_KeepsExplicitValue()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.MicrosoftOAuth;
        options.Smtp.TenantId = "test-tenant";
        options.Smtp.OAuth.TokenEndpoint = "https://explicit.example.com/token";

        _sut.PostConfigure(name: null, options);

        Assert.Equal("https://explicit.example.com/token", options.Smtp.OAuth.TokenEndpoint);
    }

    [Fact]
    public void PostConfigure_MicrosoftOAuth_DefaultsGrantTypeAndScope()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.MicrosoftOAuth;

        _sut.PostConfigure(name: null, options);

        Assert.Equal(ClientCredentialsHandler.GrantType, options.Smtp.OAuth.GrantType);
        Assert.Equal("https://outlook.office365.com/.default", options.Smtp.OAuth.Scope);
    }

    [Fact]
    public void PostConfigure_MicrosoftOAuthWithExplicitGrantTypeAndScope_KeepsExplicitValues()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.MicrosoftOAuth;
        options.Smtp.OAuth.GrantType = "explicit_grant";
        options.Smtp.OAuth.Scope = "explicit-scope";

        _sut.PostConfigure(name: null, options);

        Assert.Equal("explicit_grant", options.Smtp.OAuth.GrantType);
        Assert.Equal("explicit-scope", options.Smtp.OAuth.Scope);
    }

    [Fact]
    public void PostConfigure_MicrosoftOAuthWithShorthandClientIdAndSecret_FallsBackToShorthand()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.MicrosoftOAuth;
        options.Smtp.ClientId = "shorthand-client-id";
        options.Smtp.ClientSecret = "shorthand-client-secret";

        _sut.PostConfigure(name: null, options);

        Assert.Equal("shorthand-client-id", options.Smtp.OAuth.ClientId);
        Assert.Equal("shorthand-client-secret", options.Smtp.OAuth.ClientSecret);
    }

    [Fact]
    public void PostConfigure_MicrosoftOAuthWithExplicitOAuthClientIdAndSecret_KeepsExplicitValues()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.MicrosoftOAuth;
        options.Smtp.ClientId = "shorthand-client-id";
        options.Smtp.ClientSecret = "shorthand-client-secret";
        options.Smtp.OAuth.ClientId = "explicit-client-id";
        options.Smtp.OAuth.ClientSecret = "explicit-client-secret";

        _sut.PostConfigure(name: null, options);

        Assert.Equal("explicit-client-id", options.Smtp.OAuth.ClientId);
        Assert.Equal("explicit-client-secret", options.Smtp.OAuth.ClientSecret);
    }

    [Fact]
    public void PostConfigure_MicrosoftOAuthWithBlankOAuthClientIdAndSecretAndScope_FallsBackToShorthandAndDefaultScope()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.MicrosoftOAuth;
        options.Smtp.TenantId = "test-tenant";
        options.Smtp.ClientId = "shorthand-id";
        options.Smtp.ClientSecret = "shorthand-secret";
        options.Smtp.OAuth.ClientId = "";
        options.Smtp.OAuth.ClientSecret = "  ";
        options.Smtp.OAuth.Scope = "";
        options.Smtp.OutboundMailbox = "sender@example.com";

        _sut.PostConfigure(name: null, options);
        var result = _sut.Validate(name: null, options);

        Assert.Equal("shorthand-id", options.Smtp.OAuth.ClientId);
        Assert.Equal("shorthand-secret", options.Smtp.OAuth.ClientSecret);
        Assert.Equal("https://outlook.office365.com/.default", options.Smtp.OAuth.Scope);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MicrosoftOAuthWithAllRequiredValues_ReturnsSuccess()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.MicrosoftOAuth;
        options.Smtp.OutboundMailbox = "sender@example.com";
        options.Smtp.OAuth.TokenEndpoint = "https://login.microsoftonline.com/test-tenant/oauth2/v2.0/token";
        options.Smtp.OAuth.ClientId = "client-id";
        options.Smtp.OAuth.ClientSecret = "client-secret";

        var result = _sut.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MicrosoftOAuthMissingAllRequiredValues_FailsWithOneMessagePerField()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.MicrosoftOAuth;

        var result = _sut.Validate(name: null, options);

        Assert.False(result.Succeeded);
        Assert.Collection(
            result.Failures!,
            failure => Assert.Contains("Smtp:TenantId or Smtp:OAuth:TokenEndpoint", failure),
            failure => Assert.Contains("Smtp:OutboundMailbox", failure),
            failure => Assert.Contains("Smtp:ClientId or Smtp:OAuth:ClientId", failure),
            failure => Assert.Contains("Smtp:ClientSecret or Smtp:OAuth:ClientSecret", failure));
    }

    [Fact]
    public void PostConfigure_CustomOAuthWithShorthandClientIdAndSecret_FallsBackToShorthandAndDefaultsGrantType()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.CustomOAuth;
        options.Smtp.ClientId = "shorthand-client-id";
        options.Smtp.ClientSecret = "shorthand-client-secret";

        _sut.PostConfigure(name: null, options);

        Assert.Equal("shorthand-client-id", options.Smtp.OAuth.ClientId);
        Assert.Equal("shorthand-client-secret", options.Smtp.OAuth.ClientSecret);
        Assert.Equal(ClientCredentialsHandler.GrantType, options.Smtp.OAuth.GrantType);
    }

    [Fact]
    public void PostConfigure_CustomOAuthWithBlankOAuthClientId_FallsBackToShorthand()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.CustomOAuth;
        options.Smtp.OAuth.TokenEndpoint = "https://example.com/token";
        options.Smtp.OAuth.Scope = "custom-scope";
        options.Smtp.OutboundMailbox = "sender@example.com";
        options.Smtp.ClientId = "shorthand-client-id";
        options.Smtp.ClientSecret = "shorthand-client-secret";
        options.Smtp.OAuth.ClientId = "";

        _sut.PostConfigure(name: null, options);
        var result = _sut.Validate(name: null, options);

        Assert.Equal("shorthand-client-id", options.Smtp.OAuth.ClientId);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void PostConfigure_CustomOAuthWithExplicitValues_KeepsExplicitValues()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.CustomOAuth;
        options.Smtp.ClientId = "shorthand-client-id";
        options.Smtp.ClientSecret = "shorthand-client-secret";
        options.Smtp.OAuth.ClientId = "explicit-client-id";
        options.Smtp.OAuth.ClientSecret = "explicit-client-secret";
        options.Smtp.OAuth.GrantType = "explicit_grant";

        _sut.PostConfigure(name: null, options);

        Assert.Equal("explicit-client-id", options.Smtp.OAuth.ClientId);
        Assert.Equal("explicit-client-secret", options.Smtp.OAuth.ClientSecret);
        Assert.Equal("explicit_grant", options.Smtp.OAuth.GrantType);
    }

    [Fact]
    public void PostConfigure_CustomOAuthWithTenantIdSet_DoesNotDeriveTokenEndpointOrScope()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.CustomOAuth;
        options.Smtp.TenantId = "test-tenant";

        _sut.PostConfigure(name: null, options);

        Assert.Null(options.Smtp.OAuth.TokenEndpoint);
        Assert.Null(options.Smtp.OAuth.Scope);
    }

    [Fact]
    public void Validate_CustomOAuthWithAllRequiredValues_ReturnsSuccess()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.CustomOAuth;
        options.Smtp.OutboundMailbox = "sender@example.com";
        options.Smtp.OAuth.TokenEndpoint = "https://example.com/token";
        options.Smtp.OAuth.Scope = "custom-scope";
        options.Smtp.OAuth.ClientId = "client-id";
        options.Smtp.OAuth.ClientSecret = "client-secret";
        options.Smtp.OAuth.GrantType = ClientCredentialsHandler.GrantType;

        var result = _sut.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_CustomOAuthMissingAllRequiredValues_FailsWithOneMessagePerField()
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.CustomOAuth;

        var result = _sut.Validate(name: null, options);

        Assert.False(result.Succeeded);
        Assert.Collection(
            result.Failures!,
            failure => Assert.Contains("Smtp:OAuth:TokenEndpoint", failure),
            failure => Assert.Contains("Smtp:OAuth:Scope", failure),
            failure => Assert.Contains("Smtp:OutboundMailbox", failure),
            failure => Assert.Contains("Smtp:ClientId or Smtp:OAuth:ClientId", failure),
            failure => Assert.Contains("Smtp:ClientSecret or Smtp:OAuth:ClientSecret", failure));
    }

    [Theory]
    [InlineData(SmtpAuthType.MicrosoftOAuth, "urn:ietf:params:oauth:grant-type:jwt-bearer")]
    [InlineData(SmtpAuthType.MicrosoftOAuth, "password")]
    [InlineData(SmtpAuthType.CustomOAuth, "urn:ietf:params:oauth:grant-type:jwt-bearer")]
    [InlineData(SmtpAuthType.CustomOAuth, "password")]
    public void Validate_UnsupportedGrantType_FailsWithGrantTypeMessage(SmtpAuthType authType, string grantType)
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = authType;
        options.Smtp.OutboundMailbox = "sender@example.com";
        options.Smtp.OAuth.TokenEndpoint = "https://example.com/token";
        options.Smtp.OAuth.Scope = "custom-scope";
        options.Smtp.OAuth.ClientId = "client-id";
        options.Smtp.OAuth.ClientSecret = "client-secret";
        options.Smtp.OAuth.GrantType = grantType;

        var result = _sut.Validate(name: null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(
            $"Grant type '{grantType}' is not supported for AuthType {authType}; only " +
            $"'{ClientCredentialsHandler.GrantType}' is supported.",
            result.Failures!);
    }

    [Theory]
    [InlineData(SmtpAuthType.MicrosoftOAuth)]
    [InlineData(SmtpAuthType.CustomOAuth)]
    public void Validate_GrantTypeWrongCase_FailsWithGrantTypeMessage(SmtpAuthType authType)
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = authType;
        options.Smtp.OutboundMailbox = "sender@example.com";
        options.Smtp.OAuth.TokenEndpoint = "https://example.com/token";
        options.Smtp.OAuth.Scope = "custom-scope";
        options.Smtp.OAuth.ClientId = "client-id";
        options.Smtp.OAuth.ClientSecret = "client-secret";
        options.Smtp.OAuth.GrantType = "Client_Credentials";

        var result = _sut.Validate(name: null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(
            "Grant type 'Client_Credentials' is not supported",
            Assert.Single(result.Failures!));
    }

    [Theory]
    [InlineData(nameof(MailDeliveryOptions.SmtpOptions.OAuthOptions.TokenEndpoint))]
    [InlineData(nameof(MailDeliveryOptions.SmtpOptions.OAuthOptions.Scope))]
    [InlineData(nameof(MailDeliveryOptions.SmtpOptions.OAuthOptions.ClientId))]
    [InlineData(nameof(MailDeliveryOptions.SmtpOptions.OAuthOptions.ClientSecret))]
    public void Validate_CustomOAuthMissingSingleRequiredField_FailsNamingThatField(string missingField)
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = SmtpAuthType.CustomOAuth;
        options.Smtp.OutboundMailbox = "sender@example.com";
        options.Smtp.OAuth.TokenEndpoint = "https://example.com/token";
        options.Smtp.OAuth.Scope = "custom-scope";
        options.Smtp.OAuth.ClientId = "client-id";
        options.Smtp.OAuth.ClientSecret = "client-secret";

        switch (missingField)
        {
            case nameof(MailDeliveryOptions.SmtpOptions.OAuthOptions.TokenEndpoint):
                options.Smtp.OAuth.TokenEndpoint = null;
                break;
            case nameof(MailDeliveryOptions.SmtpOptions.OAuthOptions.Scope):
                options.Smtp.OAuth.Scope = null;
                break;
            case nameof(MailDeliveryOptions.SmtpOptions.OAuthOptions.ClientId):
                options.Smtp.OAuth.ClientId = null;
                break;
            case nameof(MailDeliveryOptions.SmtpOptions.OAuthOptions.ClientSecret):
                options.Smtp.OAuth.ClientSecret = null;
                break;
        }

        var result = _sut.Validate(name: null, options);

        Assert.False(result.Succeeded);
        var failure = Assert.Single(result.Failures!);
        Assert.Contains(missingField, failure);
    }

    [Theory]
    [InlineData(SmtpAuthType.MicrosoftOAuth, null)]
    [InlineData(SmtpAuthType.MicrosoftOAuth, "")]
    [InlineData(SmtpAuthType.MicrosoftOAuth, "   ")]
    [InlineData(SmtpAuthType.CustomOAuth, null)]
    [InlineData(SmtpAuthType.CustomOAuth, "")]
    [InlineData(SmtpAuthType.CustomOAuth, "   ")]
    public void Validate_OAuthMissingOutboundMailbox_FailsWithOutboundMailboxMessage(SmtpAuthType authType, string? outboundMailbox)
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = authType;
        options.Smtp.OutboundMailbox = outboundMailbox;
        options.Smtp.OAuth.TokenEndpoint = "https://example.com/token";
        options.Smtp.OAuth.Scope = "custom-scope";
        options.Smtp.OAuth.ClientId = "client-id";
        options.Smtp.OAuth.ClientSecret = "client-secret";

        var result = _sut.Validate(name: null, options);

        Assert.False(result.Succeeded);
        var failure = Assert.Single(result.Failures!);
        Assert.Equal(
            $"Smtp:OutboundMailbox (the sending mailbox) is required when Smtp:AuthType is {authType}.",
            failure);
    }

    [Theory]
    [InlineData(SmtpAuthType.MicrosoftOAuth)]
    [InlineData(SmtpAuthType.CustomOAuth)]
    public void Validate_OAuthWithUsernameSetButOutboundMailboxMissing_FailsWithOutboundMailboxMessage(SmtpAuthType authType)
    {
        var options = new MailDeliveryOptions();
        options.Smtp.AuthType = authType;
        options.Smtp.Username = "sender@example.com";
        options.Smtp.OAuth.TokenEndpoint = "https://example.com/token";
        options.Smtp.OAuth.Scope = "custom-scope";
        options.Smtp.OAuth.ClientId = "client-id";
        options.Smtp.OAuth.ClientSecret = "client-secret";

        var result = _sut.Validate(name: null, options);

        Assert.False(result.Succeeded);
        var failure = Assert.Single(result.Failures!);
        Assert.Equal(
            $"Smtp:OutboundMailbox (the sending mailbox) is required when Smtp:AuthType is {authType}.",
            failure);
    }
}
