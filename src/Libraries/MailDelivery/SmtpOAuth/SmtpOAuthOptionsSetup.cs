using Microsoft.Extensions.Options;

namespace Bit.Core.Platform.Mail.Delivery;

// Derives Microsoft defaults and validates required OAuth fields. Operates only on the
// already-bound MailDeliveryOptions instance: no IConfiguration dependency, per LIBRARY.md's
// "host owns binding" rule. PostConfigure derives defaults before validation runs; Validate fails
// fast with an actionable message.
internal sealed class SmtpOAuthOptionsSetup :
    IPostConfigureOptions<MailDeliveryOptions>,
    IValidateOptions<MailDeliveryOptions>
{
    public void PostConfigure(string? name, MailDeliveryOptions options)
    {
        var smtp = options.Smtp;
        if (smtp.AuthType != SmtpAuthType.MicrosoftOAuth)
        {
            return;
        }

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

    public ValidateOptionsResult Validate(string? name, MailDeliveryOptions options)
    {
        var smtp = options.Smtp;
        if (smtp.AuthType != SmtpAuthType.MicrosoftOAuth)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(smtp.OAuth.TokenEndpoint))
        {
            errors.Add("Smtp:TenantId or Smtp:OAuth:TokenEndpoint is required when Smtp:AuthType is MicrosoftOAuth.");
        }
        if (string.IsNullOrWhiteSpace(smtp.OAuth.ClientId))
        {
            errors.Add("Smtp:ClientId or Smtp:OAuth:ClientId is required when Smtp:AuthType is MicrosoftOAuth.");
        }
        if (string.IsNullOrWhiteSpace(smtp.OAuth.ClientSecret))
        {
            errors.Add("Smtp:ClientSecret or Smtp:OAuth:ClientSecret is required when Smtp:AuthType is MicrosoftOAuth.");
        }

        return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }

    private static string? OrFallback(string? value, string? fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
