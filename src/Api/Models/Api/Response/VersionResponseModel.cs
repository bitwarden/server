using System;
using Bit.Core.Models.Api;
using Bit.Core.Utilities;

namespace Bit.Web.Models.Api
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
