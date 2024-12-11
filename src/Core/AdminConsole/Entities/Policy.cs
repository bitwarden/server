using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Entities;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.AdminConsole.Entities;

public class Policy : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public PolicyType Type { get; set; }
    public string? Data { get; set; }
    public bool Enabled { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    public T GetDataModel<T>()
        where T : IPolicyDataModel, new()
    {
        return CoreHelpers.LoadClassFromJsonData<T>(Data);
    }

    public void SetDataModel<T>(T dataModel)
        where T : IPolicyDataModel, new()
    {
        Data = CoreHelpers.ClassToJsonData(dataModel);
    }
}
