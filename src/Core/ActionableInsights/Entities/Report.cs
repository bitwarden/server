#nullable enable
using Bit.Core.ActionableInsights.Models;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.ActionableInsights.Entities;

public class Report : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string? Name { get; set; }

    public Guid? GroupId { get; set; }

    public ReportType Type { get; set; }

    public required string Parameters { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        if (Id == default(Guid))
        {
            Id = CoreHelpers.GenerateComb();
        }
    }

    public ReportParameters GetParameters()
    {
        return CoreHelpers.LoadClassFromJsonData<ReportParameters>(Parameters);
    }
}
