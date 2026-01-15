using Bit.Core.AdminConsole.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class PolicyData
{
    public Guid OrganizationId { get; set; }
    public PolicyType Type { get; set; }
    public bool Enabled { get; set; }
    public string? Data { get; set; }

    public T GetDataModel<T>() where T : IPolicyDataModel, new()
    {
        return CoreHelpers.LoadClassFromJsonData<T>(Data);
    }
}
