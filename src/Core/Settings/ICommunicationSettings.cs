namespace Bit.Core.Settings;

public interface ICommunicationSettings
{
    string Bootstrap { get; set; }
    ISsoCookieVendorSettings SsoCookieVendor { get; set; }
}
