using Bit.Core.AdminConsole.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class PolicyData
{
    public Guid OrganizationId { get; init; }
    public PolicyType Type { get; init; }
    public bool Enabled { get; init; }
    public string? Data { get; init; }

    public T GetDataModel<T>() where T : IPolicyDataModel, new()
    {
        return CoreHelpers.LoadClassFromJsonData<T>(Data);
    }
}
