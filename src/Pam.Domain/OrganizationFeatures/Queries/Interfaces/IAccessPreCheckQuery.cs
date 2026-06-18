using Bit.Pam.Models;

namespace Bit.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IAccessPreCheckQuery
{
    /// <summary>
    /// Determines, without any side effects, whether the caller requesting access to the cipher would be approved
    /// automatically or would require human approval.
    /// </summary>
    Task<AccessPreCheckResult> PreCheckAsync(Guid userId, Guid cipherId);
}
