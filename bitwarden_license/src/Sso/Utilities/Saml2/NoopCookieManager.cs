using Microsoft.AspNetCore.Authentication.Cookies;

namespace Bit.Sso.Utilities.Saml2;

/// <summary>
/// A cookie manager that reads no cookie and writes no cookie.
/// </summary>
/// <remarks>
/// <see cref="SsoAuthenticationMiddleware"/> calls <see cref="Saml2OptionsExtensions.CouldHandleAsync"/>
/// for each SAML scheme, before the Sustainsys.Saml2 handler runs.
/// <c>CouldHandleAsync</c> reads the SAML message. It confirms that the message comes from the identity provider
/// of the organization, and that the message passes the key transport and signature checks.
/// To read the message, Sustainsys.Saml2 requires a cookie manager.
/// If the request has a RelayState cookie, Sustainsys.Saml2 also decrypts that cookie.
/// <c>CouldHandleAsync</c> has no decryptor for the cookie, so the read fails, and a valid login fails.
/// This cookie manager finds no cookie, so Sustainsys.Saml2 reads only the message.
/// When the message passes <c>CouldHandleAsync</c>, the middleware calls the Sustainsys.Saml2 handler
/// for the same request.
/// The handler reads the RelayState cookie, if it exists, and decrypts it with its own data protector.
/// </remarks>
internal sealed class NoopCookieManager : ICookieManager
{
    /// <summary>
    /// Returns <see langword="null"/> for each key, so the caller finds no cookie.
    /// </summary>
    public string? GetRequestCookie(HttpContext context, string key) => null;

    /// <summary>
    /// Does nothing, because this cookie manager writes no cookie.
    /// </summary>
    public void AppendResponseCookie(HttpContext context, string key, string? value, CookieOptions options)
    {
    }

    /// <summary>
    /// Does nothing, because this cookie manager writes no cookie.
    /// </summary>
    public void DeleteCookie(HttpContext context, string key, CookieOptions options)
    {
    }
}
