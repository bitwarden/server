using Bit.Core.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Sustainsys.Saml2.AspNetCore2;

namespace Bit.Sso.Utilities;

public class DynamicAuthenticationScheme : AuthenticationScheme, IDynamicAuthenticationScheme
{
    public DynamicAuthenticationScheme(string name, string displayName, Type handlerType,
        AuthenticationSchemeOptions options)
        : base(name, displayName, handlerType)
    {
        Options = options;
    }
    public DynamicAuthenticationScheme(string name, string displayName, Type handlerType,
        AuthenticationSchemeOptions options, SsoType ssoType)
        : this(name, displayName, handlerType, options)
    {
        SsoType = ssoType;
    }

    public AuthenticationSchemeOptions Options { get; set; }
    public SsoType SsoType { get; set; }

    public async Task Validate()
    {
        switch (SsoType)
        {
            case SsoType.OpenIdConnect:
                await ValidateOpenIdConnectAsync();
                break;
            case SsoType.Saml2:
                ValidateSaml();
                break;
            default:
                break;
        }
    }

    private void ValidateSaml()
    {
        if (SsoType != SsoType.Saml2)
        {
            return;
        }
        if (!(Options is Saml2Options samlOptions))
        {
            throw new Exception("InvalidAuthenticationOptionsForSaml2SchemeError");
        }
        samlOptions.Validate(Name);
    }

    private async Task ValidateOpenIdConnectAsync()
    {
        if (SsoType != SsoType.OpenIdConnect)
        {
            return;
        }
        if (!(Options is OpenIdConnectOptions oidcOptions))
        {
            throw new Exception("InvalidAuthenticationOptionsForOidcSchemeError");
        }
        oidcOptions.Validate();
        if (oidcOptions.Configuration == null)
        {
            if (oidcOptions.ConfigurationManager == null)
            {
                throw new Exception("PostConfigurationNotExecutedError");
            }
            if (oidcOptions.Configuration == null)
            {
                try
                {
                    oidcOptions.Configuration = await oidcOptions.ConfigurationManager
                        .GetConfigurationAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    throw new Exception("ReadingOpenIdConnectMetadataFailedError", ex);
                }
            }
        }
        if (oidcOptions.Configuration == null)
        {
            throw new Exception("NoOpenIdConnectMetadataError");
        }
    }
}
