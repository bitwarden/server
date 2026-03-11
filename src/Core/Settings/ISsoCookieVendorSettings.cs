namespace Bit.Core.Settings;

public interface ISsoCookieVendorSettings
{
    string IdpLoginUrl { get; set; }
    string CookieName { get; set; }
    string CookieDomain { get; set; }
}
