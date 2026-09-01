namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IDeleteAccessRuleCommand
{
    /// <summary>
    /// Hard-deletes an access rule and clears its collection links.
    /// </summary>
    /// <param name="userId">
    /// The caller, recorded as the audit event's actor. Null only if the request had no resolvable user; the event is
    /// then recorded as a system action rather than dropped.
    /// </param>
    Task DeleteAsync(Guid organizationId, Guid id, Guid? userId);
}
