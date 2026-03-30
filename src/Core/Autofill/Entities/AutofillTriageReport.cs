#nullable enable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Autofill.Entities;

public class AutofillTriageReport : ITableObject<Guid>
{
    public Guid Id { get; set; }

    [MaxLength(1024)]
    public required string PageUrl { get; set; }

    [MaxLength(512)]
    public string? TargetElementRef { get; set; }

    [MaxLength(200)]
    public string? UserMessage { get; set; }

    [MaxLength(51200)]
    public required string ReportData { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public bool Archived { get; set; } = false;

    public void SetNewId() => Id = CoreHelpers.GenerateComb();
}
