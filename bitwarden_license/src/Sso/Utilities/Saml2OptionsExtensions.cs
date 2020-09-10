using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;

namespace Bit.Sso.Utilities
{
    public static class Saml2OptionsExtensions
    {
        public static async Task<bool> CouldHandleAsync(this Saml2Options options, string scheme, HttpContext context)
        {
            // Determine this is a valid request for our handler
            if (!context.Request.Path.StartsWithSegments(options.SPOptions.ModulePath, StringComparison.Ordinal))
            {
                return false;
            }

            var idp = options.IdentityProviders.IsEmpty ? null : options.IdentityProviders.Default;
            if (idp == null)
            {
                return false;
            }

            if (context.Request.Query["scheme"].FirstOrDefault() == scheme)
            {
                return true;
            }

            // We need to pull out and parse the response or request SAML envelope
            XmlElement assertion = null;
            try
            {
                if (string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase) &&
                    context.Request.HasFormContentType)
                {
                    string encodedMessage;
                    if (context.Request.Form.TryGetValue("SAMLResponse", out var response))
                    {
                        encodedMessage = response.FirstOrDefault();
                    }
                    else
                    {
                        encodedMessage = context.Request.Form["SAMLRequest"];
                    }
                    if (string.IsNullOrWhiteSpace(encodedMessage))
                    {
                        return false;
                    }
                    assertion = XmlHelpers.XmlDocumentFromString(
                        Encoding.UTF8.GetString(Convert.FromBase64String(encodedMessage)))?.DocumentElement;
                }
                else if (string.Equals(context.Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    var encodedPayload = context.Request.Query["SAMLRequest"].FirstOrDefault() ??
                        context.Request.Query["SAMLResponse"].FirstOrDefault();
                    try
                    {
                        var payload = Convert.FromBase64String(encodedPayload);
                        using var compressed = new MemoryStream(payload);
                        using var decompressedStream = new DeflateStream(compressed, CompressionMode.Decompress, true);
                        using var deCompressed = new MemoryStream();
                        await decompressedStream.CopyToAsync(deCompressed);

                        assertion = XmlHelpers.XmlDocumentFromString(
                            Encoding.UTF8.GetString(deCompressed.GetBuffer(), 0, (int)deCompressed.Length))?.DocumentElement;
                    }
                    catch (FormatException ex)
                    {
                        throw new FormatException($"\'{encodedPayload}\' is not a valid Base64 encoded string: {ex.Message}", ex);
                    }
                }
            }
            catch
            {
                return false;
            }

            if (assertion == null)
            {
                return false;
            }

            // Double check the entity Ids
            var entityId = assertion["Issuer", Saml2Namespaces.Saml2Name]?.InnerText.Trim();
            return string.Equals(entityId, idp.EntityId.Id, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
