#nullable enable

using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

/// <summary>
/// A request for SavePolicyCommand to update a policy
/// </summary>
public record PolicyUpdate
{
    public Guid OrganizationId { get; init; }
    public PolicyType Type { get; init; }
    public string? Data { get; init; }
    public bool Enabled { get; init; }

    public T GetDataModel<T>() where T : IPolicyDataModel, new()
    {
        return CoreHelpers.LoadClassFromJsonData<T>(Data);
    }
}
