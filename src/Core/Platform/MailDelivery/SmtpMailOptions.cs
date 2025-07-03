#nullable enable

using MailKit.Security;

namespace Bit.Core.Platform.MailDelivery;

public enum AuthType
{
    Password,
    CustomOAuth,
    MicrosoftOAuth,
    GoogleOAuth,
}

public class SmtpMailOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 25;
    public bool StartTls { get; set; } = false;
    public bool Ssl { get; set; } = false;
    public bool SslOverride { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool TrustServer { get; set; } = false;
    public AuthType AuthType { get; set; }
    public OAuthMailOptions OAuth { get; set; } = new OAuthMailOptions();
    public Func<CancellationToken, Task<SaslMechanism?>> RetrieveCredentials { get; set; } = NoCredentials;

    private static Task<SaslMechanism?> NoCredentials(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<SaslMechanism?>(cancellationToken);
        }

        return Task.FromResult<SaslMechanism?>(null);
    }
}
