using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.HttpExtensions;

namespace Bit.Identity.Models.Response;

/// <summary>
/// What the Admin Portal's bulk cohort assignment upload shows after running the command: the summary when it
/// applied the file, or every error on its line (the command leaves them in its order; the page orders them by line),
/// or the one message the page shows for a refusal or failure. Exactly one of the three.
/// </summary>
public class CohortAssignmentSyncResponseModel : ResponseModel
{
    public CohortAssignmentSyncResponseModel(CohortBulkAssignmentResult result)
        : base("cohortAssignmentSync")
    {
        ArgumentNullException.ThrowIfNull(result);

        Summary = result.Succeeded ? result.Summary : null;
        Errors = result.Errors.Select(error => new CohortAssignmentSyncErrorModel(error.LineNumber, error.Message)).ToList();
    }

    public CohortAssignmentSyncResponseModel(string refusal)
        : base("cohortAssignmentSync")
    {
        Refusal = refusal;
    }

    /// <summary>What the sync changed: the command's <c>CohortBulkAssignmentSummary</c>, when it applied the file.</summary>
    public CohortBulkAssignmentSummary? Summary { get; set; }

    /// <summary>Every line-scoped error, in the command's order; nothing was written when there is any.</summary>
    public IReadOnlyList<CohortAssignmentSyncErrorModel> Errors { get; set; } = [];

    /// <summary>The command's bad-request, conflict or unhandled message, which the page shows above its form.</summary>
    public string? Refusal { get; set; }
}

/// <summary>An error on one line of the file, in the command's words.</summary>
public record CohortAssignmentSyncErrorModel(int LineNumber, string Message);
