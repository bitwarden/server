using System;
using System.IO;

namespace Bit.Setup
{
    public class AppSettingsBuilder
    {
        public void Build()
        {
            Console.WriteLine("Building app settings.");
            Directory.CreateDirectory("/bitwarden/web/");
            using(var sw = File.CreateText("/bitwarden/web/settings.js"))
            {
                sw.Write($@"var bitwardenAppSettings = {{
    iconsUri: ""/icons"",
    stripeKey: null,
    braintreeKey: null,
    selfHosted: true
}};");
            }
        }
    }
}
