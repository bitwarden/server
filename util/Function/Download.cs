using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace Bit.Function
{
    public static class Download
    {
        // Desktop
        const string DesktopCurrentVersion = "0.0.7";

        const string DesktopWindowsPortableFileName = "Bitwarden-Portable-{0}.exe";
        const string DesktopWindowsWebInstallerFileName = "Bitwarden-Installer-{0}.exe";
        const string DesktopWindowsAppxFileName = "Bitwarden-{0}.appx";
        const string DesktopWindowsAppx32FileName = "Bitwarden-{0}-ia32.appx";
        const string DesktopWindowsStoreUrl = "https://www.microsoft.com/en-us/store/b/home";
        const string DesktopWindowsChocoUrl = "https://chocolatey.org/search?q=bitwarden";

        const string DesktopMacOsDmgFileName = "Bitwarden-{0}.dmg";
        const string DesktopMacOsPkgFileName = "Bitwarden-{0}.pkg";
        const string DesktopMacOsZipFileName = "bitwarden-{0}-mac.zip";
        const string DesktopMacOsStoreUrl = "https://itunes.com";
        const string DesktopMacOsCaskUrl = "https://caskroom.github.io/search";

        const string DesktopLinuxAppImageFileName = "Bitwarden-{0}-x86_64.AppImage";
        const string DesktopLinuxDebFileName = "Bitwarden-{0}-amd64.deb";
        const string DesktopLinuxRpmFileName = "Bitwarden-{0}-x86_64.rpm";
        const string DesktopLinuxFreeBsdFileName = "Bitwarden-{0}.freebsd";
        const string DesktopLinuxSnapUrl = "https://snapcraft.io/";

        // Browser
        const string BrowserSafariFileUrl = "https://cdn.bitwarden.com/safari-extension/bitwarden-1.24.1.safariextz";
        const string BrowserSafariStoreUrl = "https://safari-extensions.apple.com";

        [FunctionName("Download")]
        public static HttpResponseMessage Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/download")]HttpRequestMessage req,
            TraceWriter log)
        {
            var qs = req.GetQueryNameValuePairs();
            var app = GetQsParam(qs, "app")?.ToLowerInvariant();
            var platform = GetQsParam(qs, "platform")?.ToLowerInvariant();
            var variant = GetQsParam(qs, "variant")?.ToLowerInvariant();

            if(string.IsNullOrWhiteSpace(app))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "'app' parameter is required.");
            }

            if(string.IsNullOrWhiteSpace(platform))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "'platform' parameter is required.");
            }

            if(app == "desktop")
            {
                if(platform == "windows")
                {
                    if(variant == null || variant == "exe" || variant == "web")
                    {
                        return GetDesktopDownloadResponse(req, DesktopWindowsWebInstallerFileName);
                    }
                    else if(variant == "portable")
                    {
                        return GetDesktopDownloadResponse(req, DesktopWindowsPortableFileName);
                    }
                    else if(variant == "appx")
                    {
                        return GetDesktopDownloadResponse(req, DesktopWindowsAppxFileName);
                    }
                    else if(variant == "appx32")
                    {
                        return GetDesktopDownloadResponse(req, DesktopWindowsAppx32FileName);
                    }
                    else if(variant == "store")
                    {
                        return GetRedirectResponse(req, DesktopWindowsStoreUrl);
                    }
                    else if(variant == "choco")
                    {
                        return GetRedirectResponse(req, DesktopWindowsChocoUrl);
                    }
                }
                else if(platform == "mac")
                {
                    if(variant == null || variant == "dmg")
                    {
                        return GetDesktopDownloadResponse(req, DesktopMacOsDmgFileName);
                    }
                    else if(variant == "pkg")
                    {
                        return GetDesktopDownloadResponse(req, DesktopMacOsPkgFileName);
                    }
                    else if(variant == "zip")
                    {
                        return GetDesktopDownloadResponse(req, DesktopMacOsZipFileName);
                    }
                    else if(variant == "store")
                    {
                        return GetRedirectResponse(req, DesktopMacOsStoreUrl);
                    }
                    else if(variant == "cask" || variant == "brew")
                    {
                        return GetRedirectResponse(req, DesktopMacOsCaskUrl);
                    }
                }
                else if(platform == "linux")
                {
                    if(variant == null || variant == "appimage")
                    {
                        return GetDesktopDownloadResponse(req, DesktopLinuxAppImageFileName);
                    }
                    else if(variant == "deb")
                    {
                        return GetDesktopDownloadResponse(req, DesktopLinuxDebFileName);
                    }
                    else if(variant == "rpm")
                    {
                        return GetDesktopDownloadResponse(req, DesktopLinuxRpmFileName);
                    }
                    else if(variant == "freebsd")
                    {
                        return GetDesktopDownloadResponse(req, DesktopLinuxFreeBsdFileName);
                    }
                    else if(variant == "snap")
                    {
                        return GetRedirectResponse(req, DesktopLinuxSnapUrl);
                    }
                }
            }
            else if(app == "browser")
            {
                if(platform == "safari")
                {
                    if(variant == null || variant == "safariextz")
                    {
                        return GetRedirectResponse(req, BrowserSafariFileUrl);
                    }
                    else if(variant == "store")
                    {
                        return GetRedirectResponse(req, BrowserSafariStoreUrl);
                    }
                }
            }

            return req.CreateResponse(HttpStatusCode.NotFound, "Download not found.");
        }

        private static string GetQsParam(IEnumerable<KeyValuePair<string, string>> qs, string key)
        {
            return qs.FirstOrDefault(q => string.Compare(q.Key, key, true) == 0).Value;
        }

        private static HttpResponseMessage GetDesktopDownloadResponse(HttpRequestMessage req, string filename)
        {
            var filenameWithVersion = string.Format(filename, DesktopCurrentVersion);
            var downloadUrl = string.Format("https://github.com/bitwarden/desktop/releases/download/v{0}/{1}",
                DesktopCurrentVersion, filenameWithVersion);
            return GetRedirectResponse(req, downloadUrl);
        }

        private static HttpResponseMessage GetRedirectResponse(HttpRequestMessage req, string url)
        {
            var response = req.CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri(url);
            return response;
        }
    }
}
