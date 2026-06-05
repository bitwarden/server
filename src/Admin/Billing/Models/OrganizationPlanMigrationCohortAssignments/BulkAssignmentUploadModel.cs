using System.ComponentModel.DataAnnotations;
using Bit.Admin.Utilities;
using Bit.Core;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bit.Admin.Billing.Models.OrganizationPlanMigrationCohortAssignments;

public class BulkAssignmentUploadModel
{
    [Required(ErrorMessage = "Choose a CSV file to upload.")]
    [MaxFileSize(Constants.FileSize25mb)]
    [Display(Name = "CSV file")]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Per-line CSV-content validation errors, rendered in the page's red error panel.
    /// Empty on first load and on a successful upload.
    /// </summary>
    [BindNever]
    public IReadOnlyList<CohortBulkAssignmentError> Errors { get; set; } = [];
}
