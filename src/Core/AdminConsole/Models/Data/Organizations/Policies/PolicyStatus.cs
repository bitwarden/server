using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Utilities;
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
        if (string.IsNullOrWhiteSpace(Data))
            return new T();
        if (ResetPasswordJsonContext.Default.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> ciTypeInfo)
            return JsonSerializer.Deserialize(Data, ciTypeInfo) ?? new T();
        if (AdminConsoleJsonContext.Default.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> typeInfo)
            return JsonSerializer.Deserialize(Data, typeInfo) ?? new T();
        return CoreHelpers.LoadClassFromJsonData<T>(Data);
    }
}
