using System;
using Bit.Core.Utilities;

namespace Bit.Core.Models.Api
{
    public class VersionResponseModel : ResponseModel
    {
        public VersionResponseModel()
            : base("version")
        {
            Version = CoreHelpers.GetVersion();
        }

        public string Version { get; set; }
    }
}
