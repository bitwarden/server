using MailKit.Security;

namespace Bit.Core.Platform.Mail.Delivery;

// Resolves SASL credentials for non-Password SmtpAuthType values and supports invalidating a
// rejected credential so the next call fetches a fresh one. AuthType.Password bypasses this
// entirely and keeps using MailKitSmtpMailDeliveryService's existing AuthenticateAsync(user, pass).
internal interface ISmtpCredentialProvider
{
    Task<SaslMechanism> GetCredentialsAsync(CancellationToken cancellationToken);

    Task InvalidateAsync(SaslMechanism rejectedCredentials, CancellationToken cancellationToken);
}
