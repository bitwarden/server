using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bit.Core.Platform.Mail.Delivery;

// Singleton: reads MailDeliveryOptions once at construction and dispatches to the right
// OAuthHandler subclass per AuthType. The OAuthHandler subclasses consume httpClientFactory,
// timeProvider, and loggerFactory.
internal sealed class SmtpCredentialProvider : ISmtpCredentialProvider
{
    private readonly OAuthHandler? _handler;

    public SmtpCredentialProvider(
        IOptions<MailDeliveryOptions> options,
        IHttpClientFactory httpClientFactory,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        var authConfig = SmtpOAuthOptionsSetup.BuildAuthConfig(options.Value.Smtp);

        // Switches on AuthType (not authConfig's type) so the compiler's CS8509 exhaustiveness
        // check still fires: pattern-matching switches over a record hierarchy are never
        // considered exhaustive by the compiler, so a type-pattern switch here would need a
        // discard arm and silently swallow a future unhandled AuthType.
        _handler = options.Value.Smtp.AuthType switch
        {
            SmtpAuthType.Password => null,
            SmtpAuthType.MicrosoftOAuth => CreateHandlerFromMicrosoftOAuth((MicrosoftOAuthConfig)authConfig),
            SmtpAuthType.CustomOAuth => CreateHandlerFromCustomOAuth((CustomOAuthConfig)authConfig),
            SmtpAuthType.GoogleOAuth => CreateHandlerFromGoogleOAuth((GoogleOAuthConfig)authConfig),
        };

        ClientCredentialsHandler CreateHandlerFromMicrosoftOAuth(MicrosoftOAuthConfig oauth) => new(
            httpClientFactory,
            timeProvider,
            loggerFactory.CreateLogger<ClientCredentialsHandler>(),
            oauth.TokenEndpoint,
            oauth.OutboundMailbox,
            oauth.ClientId,
            oauth.ClientSecret,
            oauth.Scope);

        // CustomOAuth supports two grant types, so this dispatches on GrantType (a plain string
        // switch, not subject to CS8509) rather than AuthType.
        OAuthHandler CreateHandlerFromCustomOAuth(CustomOAuthConfig oauth) => oauth.GrantType switch
        {
            JwtBearerHandler.GrantType => new JwtBearerHandler(
                httpClientFactory,
                timeProvider,
                loggerFactory.CreateLogger<JwtBearerHandler>(),
                oauth.TokenEndpoint,
                oauth.OutboundMailbox,
                oauth.SigningKey,
                oauth.Claims),
            _ => new ClientCredentialsHandler(
                httpClientFactory,
                timeProvider,
                loggerFactory.CreateLogger<ClientCredentialsHandler>(),
                oauth.TokenEndpoint,
                oauth.OutboundMailbox,
                oauth.ClientId,
                oauth.ClientSecret,
                oauth.Scope),
        };

        JwtBearerHandler CreateHandlerFromGoogleOAuth(GoogleOAuthConfig oauth) => new(
            httpClientFactory,
            timeProvider,
            loggerFactory.CreateLogger<JwtBearerHandler>(),
            oauth.TokenEndpoint,
            oauth.OutboundMailbox,
            oauth.SigningKey,
            oauth.Claims);
    }

    public Task<SaslMechanism> GetCredentialsAsync(CancellationToken cancellationToken)
    {
        if (_handler is null)
        {
            throw new InvalidOperationException(
                $"{nameof(GetCredentialsAsync)} must not be called when {nameof(SmtpAuthType)} is {nameof(SmtpAuthType.Password)}.");
        }

        return _handler.GetAsync(cancellationToken);
    }

    public Task InvalidateAsync(SaslMechanism rejectedCredentials, CancellationToken cancellationToken)
    {
        // Only OAuth2 mechanisms carry a cached access token; any other mechanism has nothing to invalidate.
        if (_handler is null || rejectedCredentials is not SaslMechanismOAuth2 oauth2)
        {
            return Task.CompletedTask;
        }

        return _handler.InvalidateIfCurrentAsync(oauth2.Credentials.Password, cancellationToken);
    }
}
