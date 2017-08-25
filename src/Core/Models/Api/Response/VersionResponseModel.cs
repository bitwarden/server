using System;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Api
{
    public class VersionResponseModel : ResponseModel
    {
        public VersionResponseModel()
            : base("version")
        {
            var info = CoreHelpers.GetVersionInfo();
            Version = info.version;
            VersionWeight = info.versionWeight;
        }

        public string Version { get; set; }
        public int VersionWeight { get; set; }
    }
}
