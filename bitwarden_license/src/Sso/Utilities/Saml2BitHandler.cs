using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.WebSso;

namespace Bit.Sso.Utilities;

// Temporary handler for validating Saml2 requests
// Most of this is taken from Sustainsys.Saml2.AspNetCore2.Saml2Handler
// TODO: PM-3641 - Remove this handler once there is a proper solution
public class Saml2BitHandler : IAuthenticationRequestHandler
{
    private readonly Saml2Handler _saml2Handler;
    private string _scheme;

    private readonly IOptionsMonitorCache<Saml2Options> _optionsCache;
    private Saml2Options _options;
    private HttpContext _context;
    private readonly IDataProtector _dataProtector;
    private readonly IOptionsFactory<Saml2Options> _optionsFactory;
    private bool _emitSameSiteNone;

    public Saml2BitHandler(
        IOptionsMonitorCache<Saml2Options> optionsCache,
        IDataProtectionProvider dataProtectorProvider,
        IOptionsFactory<Saml2Options> optionsFactory)
    {
        if (dataProtectorProvider == null)
        {
            throw new ArgumentNullException(nameof(dataProtectorProvider));
        }

        _optionsFactory = optionsFactory;
        _optionsCache = optionsCache;

        _saml2Handler = new Saml2Handler(optionsCache, dataProtectorProvider, optionsFactory);
        _dataProtector = dataProtectorProvider.CreateProtector(_saml2Handler.GetType().FullName);
    }

    public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _options = _optionsCache.GetOrAdd(scheme.Name, () => _optionsFactory.Create(scheme.Name));
        _emitSameSiteNone = _options.Notifications.EmitSameSiteNone(context.Request.GetUserAgent());
        _scheme = scheme.Name;

        return _saml2Handler.InitializeAsync(scheme, context);
    }


    public async Task<bool> HandleRequestAsync()
    {
        if (!_context.Request.Path.StartsWithSegments(_options.SPOptions.ModulePath, StringComparison.Ordinal))
        {
            return false;
        }

        var commandName = _context.Request.Path.Value.Substring(
            _options.SPOptions.ModulePath.Length).TrimStart('/');

        var commandResult = CommandFactory.GetCommand(commandName).Run(
            _context.ToHttpRequestData(_options.CookieManager, _dataProtector.Unprotect), _options);

        // Scheme is the organization ID since we use dynamic handlers for authentication schemes.
        // We need to compare this to the scheme returned in the RelayData to ensure this value hasn't been
        // tampered with
        if (commandResult.RelayData["scheme"] != _scheme)
        {
            return false;
        }

        await commandResult.Apply(
            _context, _dataProtector, _options.CookieManager, _options.SignInScheme, _options.SignOutScheme, _emitSameSiteNone);

        return true;
    }

    public Task<AuthenticateResult> AuthenticateAsync() => _saml2Handler.AuthenticateAsync();

    public Task ChallengeAsync(AuthenticationProperties properties) => _saml2Handler.ChallengeAsync(properties);

    public Task ForbidAsync(AuthenticationProperties properties) => _saml2Handler.ForbidAsync(properties);
}


static class HttpRequestExtensions
{
    public static HttpRequestData ToHttpRequestData(
        this HttpContext httpContext,
        ICookieManager cookieManager,
        Func<byte[], byte[]> cookieDecryptor)
    {
        var request = httpContext.Request;

        var uri = new Uri(
            request.Scheme
            + "://"
            + request.Host
            + request.Path
            + request.QueryString);

        var pathBase = httpContext.Request.PathBase.Value;
        pathBase = string.IsNullOrEmpty(pathBase) ? "/" : pathBase;
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> formData = null;
        if (httpContext.Request.Method == "POST" && httpContext.Request.HasFormContentType)
        {
            formData = request.Form.Select(
                f => new KeyValuePair<string, IEnumerable<string>>(f.Key, f.Value));
        }

        return new HttpRequestData(
            httpContext.Request.Method,
            uri,
            pathBase,
            formData,
            cookieName => cookieManager.GetRequestCookie(httpContext, cookieName),
            cookieDecryptor,
            httpContext.User);
    }

    public static string GetUserAgent(this HttpRequest request)
    {
        return request.Headers["user-agent"].FirstOrDefault() ?? "";
    }
}

static class CommandResultExtensions
{
    public static async Task Apply(
        this CommandResult commandResult,
        HttpContext httpContext,
        IDataProtector dataProtector,
        ICookieManager cookieManager,
        string signInScheme,
        string signOutScheme,
        bool emitSameSiteNone)
    {
        httpContext.Response.StatusCode = (int)commandResult.HttpStatusCode;

        if (commandResult.Location != null)
        {
            httpContext.Response.Headers["Location"] = commandResult.Location.OriginalString;
        }

        if (!string.IsNullOrEmpty(commandResult.SetCookieName))
        {
            var cookieData = HttpRequestData.ConvertBinaryData(
                dataProtector.Protect(commandResult.GetSerializedRequestState()));

            cookieManager.AppendResponseCookie(
                httpContext,
                commandResult.SetCookieName,
                cookieData,
                new CookieOptions()
                {
                    HttpOnly = true,
                    Secure = commandResult.SetCookieSecureFlag,
                    // We are expecting a different site to POST back to us,
                    // so the ASP.Net Core default of Lax is not appropriate in this case
                    SameSite = emitSameSiteNone ? SameSiteMode.None : (SameSiteMode)(-1),
                    IsEssential = true
                });
        }

        foreach (var h in commandResult.Headers)
        {
            httpContext.Response.Headers.Add(h.Key, h.Value);
        }

        if (!string.IsNullOrEmpty(commandResult.ClearCookieName))
        {
            cookieManager.DeleteCookie(
                httpContext,
                commandResult.ClearCookieName,
                new CookieOptions
                {
                    Secure = commandResult.SetCookieSecureFlag
                });
        }

        if (!string.IsNullOrEmpty(commandResult.Content))
        {
            var buffer = Encoding.UTF8.GetBytes(commandResult.Content);
            httpContext.Response.ContentType = commandResult.ContentType;
            await httpContext.Response.Body.WriteAsync(buffer, 0, buffer.Length);
        }

        if (commandResult.Principal != null)
        {
            var authProps = new AuthenticationProperties(commandResult.RelayData)
            {
                RedirectUri = commandResult.Location.OriginalString
            };
            await httpContext.SignInAsync(signInScheme, commandResult.Principal, authProps);
        }

        if (commandResult.TerminateLocalSession)
        {
            await httpContext.SignOutAsync(signOutScheme ?? signInScheme);
        }
    }
}
