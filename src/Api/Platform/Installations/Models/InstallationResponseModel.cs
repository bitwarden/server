using Bit.Core.Models.Api;
using Bit.Core.Platform.Installations;

namespace Bit.Api.Platform.Installations;

public class InstallationResponseModel : ResponseModel
{
    public InstallationResponseModel(Installation installation, bool withKey)
        : base("installation")
    {
        Id = installation.Id;
        Key = withKey ? installation.Key : null;
        Enabled = installation.Enabled;
    }

    public Guid Id { get; set; }
    public string Key { get; set; }
    public bool Enabled { get; set; }
}
