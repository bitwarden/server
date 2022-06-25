using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Bit.Core.Test.Utilities
{
    public class StaticStoreTests
    {
        [Fact]
        public async Task ValidateEquivalentDomainsCerts()
        {
            var enumValues = Enum.GetValues<GlobalEquivalentDomainsType>();
            // var enumValues = new [] { GlobalEquivalentDomainsType.Google, GlobalEquivalentDomainsType.Apple };

            foreach (var enumValue in enumValues)
            {
                if (!StaticStore.GlobalDomains.TryGetValue(enumValue, out var equivalentDomains))
                {
                    // The enum value does not have any equivalent domains, currently only Mozilla
                    continue;
                }

                foreach (var domain in equivalentDomains)
                {
                    var certificateResponse = await GetCertificateAsync(domain);

                    if (certificateResponse == null)
                    {
                        // A certificate could not be gethered, this could be because the site is now redirected
                        // Perhaps we should try to extract the redirect and ensure we have the redirected site
                        // as an equivalent domain
                        continue;
                    }

                    // Verify the cert
                    try
                    {
                        certificateResponse.Certificate.Verify();
                    }
                    catch (CryptographicException cryptographicEx)
                    {
                        throw new Exception($"Could not verify certificate for '{domain}'", cryptographicEx);
                    }
                }
            }
        }


        private static async Task<CertificateResponse> GetCertificateAsync(string domain)
        {
            X509Certificate2 certificate = null;
            try
            {
                using var httpClientHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, cert, _, _) =>
                    {
                        certificate = new X509Certificate2(cert.GetRawCertData());
                        return true;
                    },
                };
                using var httpClient = new HttpClient(httpClientHandler)
                {
                    Timeout = TimeSpan.FromSeconds(5),
                };

                var request = new HttpRequestMessage(HttpMethod.Head, $"https://{domain}");

                // Try https://
                var response = await httpClient.SendAsync(request);
                return new CertificateResponse(response, certificate);
            }
            catch { }

            return null;
        }
    }

    public class CertificateResponse
    {
        public HttpStatusCode StatusCode { get; }
        public X509Certificate2 Certificate { get; }
        public Uri RedirectLocation { get; }
        public bool HasCertificate => Certificate != null;
        public bool IsMovedPermanently => StatusCode == HttpStatusCode.MovedPermanently;

        public CertificateResponse(HttpResponseMessage response, X509Certificate2 certificate)
        {
            if (response is null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            Certificate = certificate;
            StatusCode = response.StatusCode;
            RedirectLocation = response.Headers.Location;
        }
    }
}
