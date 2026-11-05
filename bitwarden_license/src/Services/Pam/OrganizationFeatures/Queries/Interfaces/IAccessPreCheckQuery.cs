using Bit.Services.Pam.Models;
namespace Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;

public interface IAccessPreCheckQuery
{
    /// <summary>
    /// Determines, without side effects, whether the caller's request for the cipher would be approved automatically
    /// or need human approval.
    /// </summary>
    Task<AccessPreCheckResult> PreCheckAsync(Guid userId, Guid cipherId);
}
