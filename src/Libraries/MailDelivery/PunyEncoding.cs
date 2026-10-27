using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using MimeKit;

namespace Bit.Core.Platform.Mail.Delivery;

internal static class PunyEncoding
{
    [return: NotNullIfNotNull(nameof(text))]
    public static string? PunyEncode(string? text)
    {
        if (text == "")
        {
            return "";
        }

        if (text == null)
        {
            return null;
        }

        if (!text.Contains("@"))
        {
            // Assume domain name or non-email address
            var idn = new IdnMapping();
            return idn.GetAscii(text);
        }
        else
        {
            // Assume email address
            return MailboxAddress.EncodeAddrspec(text);
        }
    }
}
