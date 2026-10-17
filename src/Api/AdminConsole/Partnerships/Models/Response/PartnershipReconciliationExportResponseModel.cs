using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.HttpExtensions;

namespace Bit.Api.AdminConsole.Partnerships.Models.Response;

/// <summary>
/// An export of every entitlement in the partnership, one reconciliation record per line.
/// </summary>
public class PartnershipReconciliationExportResponseModel : ResponseModel
{
    public PartnershipReconciliationExportResponseModel(PartnershipReconciliationExportDetails details)
        : base("reconciliationExport")
    {
        var export = details.Export;
        Id = export.Id;
        Status = JsonNamingPolicy.SnakeCaseLower.ConvertName(export.Status.ToString());
        if (export.Status == PartnershipReconciliationExportStatus.Completed)
        {
            RecordCount = export.RecordCount;
        }

        if (details.DownloadUrl is not null)
        {
            DownloadUrl = details.DownloadUrl;
            ExpiresAt = DateTime.SpecifyKind(export.ExpirationDate, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// The export's identifier, used to poll it.
    /// </summary>
    public Guid Id { get; }
    /// <summary>
    /// One of <c>pending</c>, <c>running</c>, <c>completed</c>, or <c>failed</c>.
    /// </summary>
    public string Status { get; }
    /// <summary>
    /// The number of entitlements in the file. Present once the export has completed.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RecordCount { get; }
    /// <summary>
    /// A short-lived, read-only URL for the newline-delimited JSON file. Request the export again for a new one.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DownloadUrl { get; }
    /// <summary>
    /// When the export and its file are deleted.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? ExpiresAt { get; }
}
