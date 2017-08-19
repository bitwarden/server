using System;
using System.Reflection;

namespace Bit.Core.Models.Api
{
    public class VersionResponseModel : ResponseModel
    {
        public VersionResponseModel()
            : base("version")
        {
            Version = Assembly.GetEntryAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion;
            VersionInt = Convert.ToInt32(Version.Replace(".", string.Empty));
        }

        public string Version { get; set; }
        public int VersionInt { get; set; }
    }
}
