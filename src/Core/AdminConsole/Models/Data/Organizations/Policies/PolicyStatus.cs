using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

public class PolicyStatus
{
    public PolicyStatus(Guid organizationId, PolicyType policyType, Policy? policy = null)
    {
        OrganizationId = policy?.OrganizationId ?? organizationId;
        Data = policy?.Data;
        Type = policy?.Type ?? policyType;
        Enabled = policy?.Enabled ?? false;
    }

    public Guid OrganizationId { get; set; }
    public PolicyType Type { get; set; }
    public bool Enabled { get; set; }
    public string? Data { get; set; }

    public T GetDataModel<T>() where T : IPolicyDataModel, new()
    {
        return CoreHelpers.LoadClassFromJsonData<T>(Data);
    }
}
