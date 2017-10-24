using System;
using System.IO;

namespace Bit.Setup
{
    public class AppIdBuilder
    {
        public AppIdBuilder(string url)
        {
            Url = url;
        }

        public string Url { get; private set; }

        public void Build()
        {
            Console.WriteLine("Building FIDO U2F app id.");
            Directory.CreateDirectory("/bitwarden/web/");
            using(var sw = File.CreateText("/bitwarden/web/app-id.json"))
            {
                sw.Write($@"{{
  ""trustedFacets"": [
    {{
      ""version"": {{
        ""major"": 1,
        ""minor"": 0
      }},
      ""ids"": [
        ""{Url}"",
        ""ios:bundle-id:com.8bit.bitwarden"",
        ""android:apk-key-hash:dUGFzUzf3lmHSLBDBIv+WaFyZMI""
      ]
    }}
  ]
}}");
            }
        }
    }
}
