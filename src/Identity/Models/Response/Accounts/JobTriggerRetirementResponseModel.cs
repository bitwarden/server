using Bit.Core.Models.Api;

namespace Bit.Identity.Models.Response.Accounts;

/// <summary>What the internal job trigger retirement did: counts, and nothing of the rows.</summary>
public class JobTriggerRetirementResponseModel : ResponseModel
{
    public JobTriggerRetirementResponseModel(
        int emergencyAccessDeleted,
        int domainClaimsDeleted,
        int cohortAssignmentsDeleted,
        int organizationsSeatSyncCleared)
        : base("jobTriggerRetirement")
    {
        EmergencyAccessDeleted = emergencyAccessDeleted;
        DomainClaimsDeleted = domainClaimsDeleted;
        CohortAssignmentsDeleted = cohortAssignmentsDeleted;
        OrganizationsSeatSyncCleared = organizationsSeatSyncCleared;
    }

    /// <summary>The emergency access grants deleted, the users' as grantor or as grantee.</summary>
    public int EmergencyAccessDeleted { get; set; }

    /// <summary>The organizations' unverified domain claims deleted; verified ones are kept.</summary>
    public int DomainClaimsDeleted { get; set; }

    /// <summary>The organizations' price migration cohort assignments deleted.</summary>
    public int CohortAssignmentsDeleted { get; set; }

    /// <summary>The distinct organizations whose seat sync was cleared, whether or not one was pending.</summary>
    public int OrganizationsSeatSyncCleared { get; set; }
}
