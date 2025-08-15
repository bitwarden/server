#nullable enable

using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
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
    public IActingUser? PerformedBy { get; set; }

    public T GetDataModel<T>() where T : IPolicyDataModel, new()
    {
        return CoreHelpers.LoadClassFromJsonData<T>(Data);
    }

    public void SetDataModel<T>(T dataModel) where T : IPolicyDataModel, new()
    {
        Data = CoreHelpers.ClassToJsonData(dataModel);
    }
}

public class SavePolicyRequest(PolicyUpdate data, IPolicyMetadataModel metadata, PolicyContext policyContext)
{
    public PolicyUpdate Data { get; set; } = data;
    public IPolicyMetadataModel Metadata { get; set; } = metadata;
    public PolicyContext PolicyContext { get; set; } = policyContext;
}


public class OrganizationModelOwnershipPolicyModel : IPolicyMetadataModel
{

    public OrganizationModelOwnershipPolicyModel(string defaultUserCollectionName, bool executeSideEffect)
    {
        ExecuteSideEffect = executeSideEffect;
        DefaultUserCollectionName = defaultUserCollectionName;
    }

    public string DefaultUserCollectionName { get; set; }

    public bool ExecuteSideEffect { get; set; }
}

public interface IPolicyMetadataModel
{

}

public class PolicyContext(IActingUser? performedBy)
{
    public IActingUser? PerformedBy { get; set; } = performedBy;
}
