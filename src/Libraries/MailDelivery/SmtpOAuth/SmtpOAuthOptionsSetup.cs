using Microsoft.Extensions.Options;

namespace Bit.Core.Platform.Mail.Delivery;

// Derives per-provider defaults and validates required OAuth fields for MicrosoftOAuth and
// CustomOAuth. Operates only on the already-bound MailDeliveryOptions instance: no IConfiguration
// dependency, per LIBRARY.md's "host owns binding" rule. PostConfigure derives defaults before
// validation runs; Validate fails fast with an actionable message, then builds the matching
// SmtpAuthConfig now that its required fields are known to be present.
// PostConfigure, Validate, and BuildAuthConfig dispatch through switch expressions, so a new
// SmtpAuthType fails the build (CS8509) until it is handled here.
internal sealed class SmtpOAuthOptionsSetup :
    IPostConfigureOptions<MailDeliveryOptions>,
    IValidateOptions<MailDeliveryOptions>
{
    public void PostConfigure(string? name, MailDeliveryOptions options)
    {
        Action<MailDeliveryOptions.SmtpOptions> deriveDefaults = options.Smtp.AuthType switch
        {
            SmtpAuthType.Password => DeriveNoDefaults,
            SmtpAuthType.MicrosoftOAuth => DeriveMicrosoftDefaults,
            SmtpAuthType.CustomOAuth => DeriveCustomDefaults,
        };
        deriveDefaults(options.Smtp);
    }

    public ValidateOptionsResult Validate(string? name, MailDeliveryOptions options)
    {
        List<string> errors = options.Smtp.AuthType switch
        {
            SmtpAuthType.Password => [],
            SmtpAuthType.MicrosoftOAuth => MicrosoftErrors(options.Smtp),
            SmtpAuthType.CustomOAuth => CustomErrors(options.Smtp),
        };
        if (errors.Count > 0)
        {
            return ValidateOptionsResult.Fail(errors);
        }

        // Validation above has already enforced the invariants each SmtpAuthConfig's required
        // members encode, so construction here cannot fail. The result isn't wired into a
        // consumer yet; that lands in the next TODO (SmtpCredentialProvider).
        _ = BuildAuthConfig(options.Smtp);
        return ValidateOptionsResult.Success;
    }

    // Converts the already-validated SmtpOptions into the SmtpAuthConfig matching its AuthType.
    // Exposed internally so SmtpCredentialProvider can call it directly once it's updated to
    // consume SmtpAuthConfig instead of raw SmtpOptions.
    internal static SmtpAuthConfig BuildAuthConfig(MailDeliveryOptions.SmtpOptions smtp) => smtp.AuthType switch
    {
        SmtpAuthType.Password => new PasswordAuthConfig
        {
            Username = smtp.Username!,
            Password = smtp.Password!,
        },
        SmtpAuthType.MicrosoftOAuth => new MicrosoftOAuthConfig
        {
            TokenEndpoint = smtp.OAuth.TokenEndpoint!,
            OutboundMailbox = smtp.OutboundMailbox!,
            ClientId = smtp.OAuth.ClientId!,
            ClientSecret = smtp.OAuth.ClientSecret!,
            Scope = smtp.OAuth.Scope!,
            GrantType = smtp.OAuth.GrantType!,
        },
        SmtpAuthType.CustomOAuth => new CustomOAuthConfig
        {
            TokenEndpoint = smtp.OAuth.TokenEndpoint!,
            OutboundMailbox = smtp.OutboundMailbox!,
            ClientId = smtp.OAuth.ClientId!,
            ClientSecret = smtp.OAuth.ClientSecret!,
            Scope = smtp.OAuth.Scope!,
            GrantType = smtp.OAuth.GrantType!,
        },
    };

    private static void DeriveNoDefaults(MailDeliveryOptions.SmtpOptions smtp) { }

    private static void DeriveMicrosoftDefaults(MailDeliveryOptions.SmtpOptions smtp)
    {
        // Token endpoint format: https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-client-creds-grant-flow#first-case-access-token-request-with-a-shared-secret
        if (string.IsNullOrWhiteSpace(smtp.OAuth.TokenEndpoint) && !string.IsNullOrWhiteSpace(smtp.TenantId))
        {
            smtp.OAuth.TokenEndpoint = $"https://login.microsoftonline.com/{smtp.TenantId}/oauth2/v2.0/token";
        }

        // Exchange requires this scope for client-credentials SMTP: https://learn.microsoft.com/en-us/exchange/client-developer/legacy-protocols/how-to-authenticate-an-imap-pop-smtp-application-by-using-oauth#use-client-credentials-grant-flow-to-authenticate-smtp-imap-and-pop-connections
        smtp.OAuth.Scope = OrFallback(smtp.OAuth.Scope, "https://outlook.office365.com/.default");
        smtp.OAuth.GrantType = OrFallback(smtp.OAuth.GrantType, ClientCredentialsHandler.GrantType);
        smtp.OAuth.ClientId = OrFallback(smtp.OAuth.ClientId, smtp.ClientId);
        smtp.OAuth.ClientSecret = OrFallback(smtp.OAuth.ClientSecret, smtp.ClientSecret);
    }

    private static void DeriveCustomDefaults(MailDeliveryOptions.SmtpOptions smtp)
    {
        smtp.OAuth.ClientId = OrFallback(smtp.OAuth.ClientId, smtp.ClientId);
        smtp.OAuth.ClientSecret = OrFallback(smtp.OAuth.ClientSecret, smtp.ClientSecret);
        smtp.OAuth.GrantType = OrFallback(smtp.OAuth.GrantType, ClientCredentialsHandler.GrantType);
    }

    private static List<string> MicrosoftErrors(MailDeliveryOptions.SmtpOptions smtp)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(smtp.OAuth.TokenEndpoint))
        {
            errors.Add("Smtp:TenantId or Smtp:OAuth:TokenEndpoint is required when Smtp:AuthType is MicrosoftOAuth.");
        }
        AddClientCredentialErrors(smtp, errors);
        AddGrantTypeErrors(smtp, errors);
        return errors;
    }

    private static List<string> CustomErrors(MailDeliveryOptions.SmtpOptions smtp)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(smtp.OAuth.TokenEndpoint))
        {
            errors.Add("Smtp:OAuth:TokenEndpoint is required when Smtp:AuthType is CustomOAuth.");
        }
        if (string.IsNullOrWhiteSpace(smtp.OAuth.Scope))
        {
            errors.Add("Smtp:OAuth:Scope is required when Smtp:AuthType is CustomOAuth.");
        }
        AddClientCredentialErrors(smtp, errors);
        AddGrantTypeErrors(smtp, errors);
        return errors;
    }

    private static string? OrFallback(string? value, string? fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static void AddClientCredentialErrors(MailDeliveryOptions.SmtpOptions smtp, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(smtp.OutboundMailbox))
        {
            errors.Add($"Smtp:OutboundMailbox (the sending mailbox) is required when Smtp:AuthType is {smtp.AuthType}.");
        }
        if (string.IsNullOrWhiteSpace(smtp.OAuth.ClientId))
        {
            errors.Add($"Smtp:ClientId or Smtp:OAuth:ClientId is required when Smtp:AuthType is {smtp.AuthType}.");
        }
        if (string.IsNullOrWhiteSpace(smtp.OAuth.ClientSecret))
        {
            errors.Add($"Smtp:ClientSecret or Smtp:OAuth:ClientSecret is required when Smtp:AuthType is {smtp.AuthType}.");
        }
    }

    private static void AddGrantTypeErrors(MailDeliveryOptions.SmtpOptions smtp, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(smtp.OAuth.GrantType))
        {
            return;
        }

        if (smtp.OAuth.GrantType != ClientCredentialsHandler.GrantType)
        {
            errors.Add(
                $"Grant type '{smtp.OAuth.GrantType}' is not supported for AuthType {smtp.AuthType}; only '{ClientCredentialsHandler.GrantType}' is supported.");
        }
    }
}
