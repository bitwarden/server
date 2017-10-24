using System;
using System.IO;

namespace Bit.Setup
{
    public class AppSettingsBuilder
    {
        public AppSettingsBuilder(string url, string domain)
        {
            Url = url;
            Domain = domain;
        }

        public string Url { get; private set; }
        public string Domain { get; private set; }

        public void Build()
        {
            Console.WriteLine("Building app settings.");
            Directory.CreateDirectory("/bitwarden/web/");
            using(var sw = File.CreateText("/bitwarden/web/settings.js"))
            {
                sw.Write($@"// Config Parameters
// Parameter:Url={Url}
// Parameter:Domain={Domain}

var bitwardenAppSettings = {{
    apiUri: ""{Url}/api"",
    identityUri: ""{Url}/identity"",
    iconsUri: ""{Url}/icons"",
    stripeKey: null,
    braintreeKey: null,
    whitelistDomains: [""{Domain}""],
    selfHosted: true
}};");
            }
        }
    }
}
