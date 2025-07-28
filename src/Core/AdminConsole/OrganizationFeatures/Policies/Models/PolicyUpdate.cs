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



public class ProtoTypeEnvelopeModel<TDateType>(TDateType data, IMetadata metadata)
{
    public TDateType Data { get; set; } = data;

    public IMetadata Metadata { get; set; } = metadata;
}

public class SingleOrganizationPolicyMetadataModel : IMetadata
{
    public SingleOrganizationPolicyMetadataModel(string defaultCollectionName)
    {
        DefaultCollectionName = defaultCollectionName;
    }

    public string DefaultCollectionName { get; set; }
}

public interface IMetadata
{
}
