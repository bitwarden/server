using System;

namespace Bit.Core.Models.Mail
{
    public class BaseMailModel
    {
        public string SiteName { get; set; }
        public string WebVaultUrl { get; set; }
        public string WebVaultUrlHostname
        {
            get
            {
                if (Uri.TryCreate(WebVaultUrl, UriKind.Absolute, out Uri uri))
                {
                    return uri.Host;
                }

                return WebVaultUrl;
            }
        }
    }
}
