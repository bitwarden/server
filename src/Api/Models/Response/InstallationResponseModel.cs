using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response;

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
