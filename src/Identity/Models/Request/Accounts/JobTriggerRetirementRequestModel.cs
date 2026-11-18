#nullable enable

namespace Bit.Identity.Models.Request.Accounts;

/// <summary>
/// The users and organizations whose job triggers this install retires: another platform now serves them. Either list
/// may be empty or absent.
/// </summary>
public class JobTriggerRetirementRequestModel
{
    /// <summary>The users whose emergency access grants, as grantor or grantee, go.</summary>
    public List<Guid>? UserIds { get; set; }

    /// <summary>The organizations whose domain claims, price migration cohort assignment and seat sync go.</summary>
    public List<Guid>? OrganizationIds { get; set; }
}
