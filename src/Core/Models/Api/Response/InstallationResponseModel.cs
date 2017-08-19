using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class InstallationResponseModel : ResponseModel
    {
        public InstallationResponseModel(Installation installation, bool withKey)
            : base("installation")
        {
            Id = installation.Id.ToString();
            Key = withKey ? installation.Key : null;
            Enabled = installation.Enabled;
        }

        public string Id { get; set; }
        public string Key { get; set; }
        public bool Enabled { get; set; }
    }
}
