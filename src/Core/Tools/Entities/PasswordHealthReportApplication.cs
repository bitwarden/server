using Bit.Core.Entities;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Tools.Entities;

public class PasswordHealthReportApplication : ITableObject<Guid>, IRevisable
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string? Uri { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
