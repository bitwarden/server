namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IDeleteAccessRuleCommand
{
    /// <summary>Hard-deletes an access rule and clears its collection links.</summary>
    /// <param name="userId">The caller, recorded as the audit actor. Null records a system action.</param>
    Task DeleteAsync(Guid organizationId, Guid id, Guid? userId);
}
