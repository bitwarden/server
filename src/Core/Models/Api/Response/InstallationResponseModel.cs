using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Api
{
    public class InstallationResponseModel : ResponseModel
    {
        public InstallationResponseModel(Installation installation)
            : base("installation")
        {
            Id = installation.Id.ToString();
            Key = installation.Key;
        }

        public string Id { get; set; }
        public string Key { get; set; }
    }
}
