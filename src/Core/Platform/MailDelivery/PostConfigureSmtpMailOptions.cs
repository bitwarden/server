#nullable enable

using Bit.Core.Utilities;
using IdentityModel;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Bit.Core.Platform.MailDelivery;

public class OAuthMailOptions
{
    public string? TokenEndpoint { get; set; }
    public string? GrantType { get; set; }

    // JWT Properties
    public string? Algorithm { get; set; }
    public Dictionary<string, string?> Claims { get; set; } = [];
    public string? SigningKey { get; set; }

    // client_credentials properties
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Scope { get; set; }
}

internal class PostConfigureSmtpMailOptions : IPostConfigureOptions<SmtpMailOptions>
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;

    public PostConfigureSmtpMailOptions(
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        TimeProvider timeProvider
    )
    {
        // Use a custom, simpler category name in case we ever want to document this to help
        // customers diagnose mail issues.
        _logger = loggerFactory.CreateLogger("Bit.Mail");
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _timeProvider = timeProvider;
    }

    public void PostConfigure(string? name, SmtpMailOptions options)
    {
        if (name != Options.DefaultName)
        {
            // We only configure the default options
            return;
        }

        var smtpOptions = _configuration.GetSection("GlobalSettings:Mail:Smtp");

        switch (options.AuthType)
        {
            // Read special cased configuration into the general config
            case AuthType.MicrosoftOAuth:

                if (string.IsNullOrEmpty(options.OAuth.TokenEndpoint))
                {
                    // Get tenant
                    var tenantId = smtpOptions.GetValue<string>("TenantId")
                        ?? throw OptionsException(
                            "A GlobalSettings:Mail:Smtp:TenantId or GlobalSettings:Mail:Smtp:OAuth:TokenEndpoint is" +
                            "required to be set when AuthType is MicrosoftOAuth"
                        );
                    // Format tenant into default url
                    options.OAuth.TokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
                }
                options.OAuth.GrantType ??= ClientCredentialsHandler.GrantType;
                // Default to known scope
                options.OAuth.Scope ??= "https://outlook.office365.com/.default";
                // Allow Shortened options
                options.OAuth.ClientId ??= smtpOptions.GetValue<string>("ClientId")
                    ?? throw OptionsException(
                        "A GlobalSettings:Mail:Smtp:ClientId or GlobalSettings:Mail:Smtp:OAuth:ClientId is" +
                        "required to be set when AuthType is MicrosoftOAuth"
                    );
                options.OAuth.ClientSecret ??= smtpOptions.GetValue<string>("ClientSecret")
                    ?? throw OptionsException(
                        "A GlobalSettings:Mail:Smtp:ClientSecret or GlobalSettings:Mail:Smtp:OAuth:ClientSecret is" +
                        "required to be set when AuthType is MicrosoftOAuth"
                    );
                break;
            case AuthType.GoogleOAuth:
                // Default the Google URL
                options.OAuth.TokenEndpoint ??= "https://oauth2.googleapis.com/token";
                // Default to known default grant type for Google
                options.OAuth.GrantType ??= JwtBearerCredentials.GrantType;
                // Default the algorithm
                options.OAuth.Algorithm ??= SecurityAlgorithms.RsaSha256;
                // Allow shortened path and more recognizable name for signing key
                options.OAuth.SigningKey ??= smtpOptions.GetValue<string>("ServiceAccountPrivateKey")
                    ?? throw OptionsException(
                        "A GlobalSettings:Mail:Smtp:ServiceAccountPrivateKey or GlobalSettings:Mail:Smtp:OAuth:SigningKey" +
                        "is required to be set when AuthType is GoogleOAuth"
                    );

                // Allow more recognizable name to be used as the iss claim if not already included.
                if (!options.OAuth.Claims.ContainsKey(JwtClaimTypes.Issuer))
                {
                    var serviceAccountEmail = smtpOptions.GetValue<string>("ServiceAccountEmail")
                        ?? throw OptionsException(
                            "A GlobalSettings:Mail:Smtp:ServiceAccountEmail or GlobalSettings:Mail:Smtp:OAuth:Claims:iss" +
                            "is required to be set when AuthType is GoogleOAuth"
                        );
                    options.OAuth.Claims.Add(JwtClaimTypes.Issuer, serviceAccountEmail);
                }

                // Add known scope for Google if not already found
                options.OAuth.Claims.TryAdd(JwtClaimTypes.Scope, "https://mail.google.com/");
                // Add known aud for Google if not already found
                options.OAuth.Claims.TryAdd(JwtClaimTypes.Audience, "https://oauth2.googleapis.com/token");
                break;
        }

        // Build credential builder
        if (options.AuthType == AuthType.Password)
        {
            if (CoreHelpers.SettingHasValue(options.Username) && CoreHelpers.SettingHasValue(options.Password))
            {
                options.RetrieveCredentials = (token) =>
                {
                    return Task.FromResult<SaslMechanism?>(new SaslMechanismLogin(options.Username, options.Password));
                };
            }
            return;
        }

        if (options.OAuth.GrantType == ClientCredentialsHandler.GrantType)
        {
            // TODO: Final validation
            var clientCredentialsHandler = new ClientCredentialsHandler(
                _httpClientFactory,
                _timeProvider,
                options.OAuth.TokenEndpoint!,
                options.Username!,
                options.OAuth.ClientId!,
                options.OAuth.ClientSecret!,
                options.OAuth.Scope!
            );

            options.RetrieveCredentials = clientCredentialsHandler.GetAsync;
        }
        else if (options.OAuth.GrantType == JwtBearerCredentials.GrantType)
        {
            // TODO: Final validation
            var jwtBearer = new JwtBearerCredentials(
                _httpClientFactory,
                _timeProvider,
                options.OAuth.TokenEndpoint!,
                options.Username!,
                options.OAuth.Algorithm ?? SecurityAlgorithms.RsaSha256,
                options.OAuth.SigningKey!,
                options.OAuth.Claims
            );

            options.RetrieveCredentials = jwtBearer.GetAsync;
        }
        else
        {
            _logger.LogWarning(
                "Grant type '{GrantType}' is not one of the supported values 'client_credentials' or 'urn:ietf:params:oauth:grant-type:jwt-bearer'",
                options.OAuth.GrantType
            );
        }
    }

    private static OptionsValidationException OptionsException(string error)
    {
        return new OptionsValidationException(
            // We don't currently customize any options other than the default one, 
            // if that changes we'll need to take the name as a parameter.
            Options.DefaultName,
            typeof(SmtpMailOptions),
            [error]
        );
    }
}
