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
        var smtp = options.Value.Smtp;

        _handler = smtp.AuthType switch
        {
            SmtpAuthType.Password => null,
            SmtpAuthType.MicrosoftOAuth => new ClientCredentialsHandler(
                httpClientFactory,
                timeProvider,
                loggerFactory.CreateLogger<ClientCredentialsHandler>(),
                smtp.OAuth.TokenEndpoint!,
                smtp.OutboundMailbox!,
                smtp.OAuth.ClientId!,
                smtp.OAuth.ClientSecret!,
                smtp.OAuth.Scope!),
            SmtpAuthType.CustomOAuth => new ClientCredentialsHandler(
                httpClientFactory,
                timeProvider,
                loggerFactory.CreateLogger<ClientCredentialsHandler>(),
                smtp.OAuth.TokenEndpoint!,
                smtp.OutboundMailbox!,
                smtp.OAuth.ClientId!,
                smtp.OAuth.ClientSecret!,
                smtp.OAuth.Scope!),
        };
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
