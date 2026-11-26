namespace Bit.AppHost.Configuration;

/// <summary>
/// Settings for the MailCatcher container.
/// </summary>
internal sealed record MailCatcherSettings : ContainerImageSettings
{
    /// <summary>
    /// Image reference in <c>name[:tag]</c> form; the key is required.
    /// </summary>
    internal override required string Image { get; init; }

    /// <summary>
    /// Host port the Bitwarden services send SMTP mail to.
    /// </summary>
    internal required int SmtpPort { get; init; }

    /// <summary>
    /// Host port of the web inbox.
    /// </summary>
    internal required int WebPort { get; init; }
}
