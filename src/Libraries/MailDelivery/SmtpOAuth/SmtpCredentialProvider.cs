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
            // Thrown eagerly here, not lazily in GetCredentialsAsync, because this provider is
            // constructed once as a singleton and should fail fast rather than fail on the
            // first send attempt.
            _ => throw new NotSupportedException(
                $"SmtpAuthType '{smtp.AuthType}' is not supported."),
        };

        _ = httpClientFactory;
        _ = timeProvider;
        _ = loggerFactory;
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
