using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Models.Data;

public class ProjectPermissionDetails
{
    public Project Project { get; set; }
    public bool Read { get; set; }
    public bool Write { get; set; }
}
