using Bit.Core.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Sustainsys.Saml2.AspNetCore2;

namespace Bit.Sso.Utilities;

public class SsoAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public SsoAuthenticationMiddleware(RequestDelegate next, IAuthenticationSchemeProvider schemes)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        Schemes = schemes ?? throw new ArgumentNullException(nameof(schemes));
    }

    public IAuthenticationSchemeProvider Schemes { get; set; }

    public async Task Invoke(HttpContext context)
    {
        if ((context.Request.Method == "GET" && context.Request.Query.ContainsKey("SAMLart"))
            || (context.Request.Method == "POST" && context.Request.Form.ContainsKey("SAMLart")))
        {
            throw new Exception("SAMLart parameter detected. SAML Artifact binding is not allowed.");
        }

        context.Features.Set<IAuthenticationFeature>(new AuthenticationFeature
        {
            OriginalPath = context.Request.Path,
            OriginalPathBase = context.Request.PathBase
        });

        // Give any IAuthenticationRequestHandler schemes a chance to handle the request
        var handlers = context.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
        foreach (var scheme in await Schemes.GetRequestHandlerSchemesAsync())
        {
            // Determine if scheme is appropriate for the current context FIRST
            if (scheme is IDynamicAuthenticationScheme dynamicScheme)
            {
                switch (dynamicScheme.SsoType)
                {
                    case SsoType.OpenIdConnect:
                    default:
                        if (dynamicScheme.Options is OpenIdConnectOptions oidcOptions &&
                            !await oidcOptions.CouldHandleAsync(scheme.Name, context))
                        {
                            // It's OIDC and Dynamic, but not a good fit
                            continue;
                        }
                        break;
                    case SsoType.Saml2:
                        if (dynamicScheme.Options is Saml2Options samlOptions &&
                            !await samlOptions.CouldHandleAsync(scheme.Name, context))
                        {
                            // It's SAML and Dynamic, but not a good fit
                            continue;
                        }
                        break;
                }
            }

            // This far it's not dynamic OR it is but "could" be handled
            if (await handlers.GetHandlerAsync(context, scheme.Name) is IAuthenticationRequestHandler handler &&
                await handler.HandleRequestAsync())
            {
                return;
            }
        }

        // Fallback to the default scheme from the provider
        var defaultAuthenticate = await Schemes.GetDefaultAuthenticateSchemeAsync();
        if (defaultAuthenticate != null)
        {
            var result = await context.AuthenticateAsync(defaultAuthenticate.Name);
            if (result?.Principal != null)
            {
                context.User = result.Principal;
            }
        }

        await _next(context);
    }
}
