#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;

/// <summary>
/// A request for SavePolicyCommand to update a policy
/// </summary>
public record PolicyUpdate
{
    public Guid OrganizationId { get; set; }
    public PolicyType Type { get; set; }
    public string? Data { get; set; }
    public bool Enabled { get; set; }

    [Obsolete("Please use SavePolicyModel.PerformedBy instead.")]
    public IActingUser? PerformedBy { get; set; }

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

    public void SetDataModel<T>(T dataModel) where T : IPolicyDataModel, new()
    {
        if (AdminConsoleJsonContext.Default.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> typeInfo)
            Data = JsonSerializer.Serialize(dataModel, typeInfo);
        else
            Data = CoreHelpers.ClassToJsonData(dataModel);
    }
}
