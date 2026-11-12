namespace Bit.Core.Platform.Mail.Delivery;

// Replaces MailDeliveryOptions.SmtpOptions' optional-but-conditionally-required OAuth fields
// with one concrete, fully-required type per SmtpAuthType value. SmtpOAuthOptionsSetup will
// construct these after PostConfigure/Validate has already enforced the invariants each type's
// required members encode; no IConfiguration reads or network calls happen here or in that
// construction. A jwt-bearer concrete type (Google/Custom) is PR 2 scope and is intentionally
// not present yet.
internal abstract record SmtpAuthConfig;

internal sealed record PasswordAuthConfig : SmtpAuthConfig
{
    public required string Username { get; init; }
    public required string Password { get; init; }

    // Overrides the generated record ToString, which prints every property, to keep the
    // password out of logs.
    public override string ToString() =>
        $"{nameof(PasswordAuthConfig)} {{ Username = {Username}, Password = [redacted] }}";
}

internal sealed record MicrosoftOAuthConfig : SmtpAuthConfig
{
    public required string TokenEndpoint { get; init; }
    public required string OutboundMailbox { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string Scope { get; init; }
    public required string GrantType { get; init; }

    // Overrides the generated record ToString, which prints every property, to keep the client
    // secret out of logs.
    public override string ToString() =>
        $"{nameof(MicrosoftOAuthConfig)} {{ TokenEndpoint = {TokenEndpoint}, OutboundMailbox = {OutboundMailbox}, " +
        $"ClientId = {ClientId}, ClientSecret = [redacted], Scope = {Scope}, GrantType = {GrantType} }}";
}

internal sealed record CustomOAuthConfig : SmtpAuthConfig
{
    public required string TokenEndpoint { get; init; }
    public required string OutboundMailbox { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string Scope { get; init; }
    public required string GrantType { get; init; }

    // Used only when GrantType is JwtBearerHandler.GrantType; unused for client_credentials.
    public required string SigningKey { get; init; }
    public required IReadOnlyDictionary<string, string> Claims { get; init; }

    // Overrides the generated record ToString, which prints every property, to keep the client
    // secret and signing key out of logs.
    public override string ToString() =>
        $"{nameof(CustomOAuthConfig)} {{ TokenEndpoint = {TokenEndpoint}, OutboundMailbox = {OutboundMailbox}, " +
        $"ClientId = {ClientId}, ClientSecret = [redacted], Scope = {Scope}, GrantType = {GrantType}, " +
        $"SigningKey = [redacted], Claims = [redacted] }}";
}

internal sealed record GoogleOAuthConfig : SmtpAuthConfig
{
    public required string TokenEndpoint { get; init; }
    public required string OutboundMailbox { get; init; }
    public required string SigningKey { get; init; }
    public required IReadOnlyDictionary<string, string> Claims { get; init; }

    // Overrides the generated record ToString, which prints every property, to keep the
    // signing key and claims out of logs.
    public override string ToString() =>
        $"{nameof(GoogleOAuthConfig)} {{ TokenEndpoint = {TokenEndpoint}, OutboundMailbox = {OutboundMailbox}, " +
        $"SigningKey = [redacted], Claims = [redacted] }}";
}
