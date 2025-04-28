using Bit.Core.Entities;
namespace Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IInitPendingOrganizationCommand
{
    /// <summary>
    /// Accept an invitation to initialize and join an organization created via the Admin Portal
    /// </summary>
    /// <remarks>
    /// This method must target a disabled Organization that has null keys and status as 'Pending'.
    /// </remarks>
    Task InitPendingOrganizationAsync(User user, Guid organizationId, Guid organizationUserId, string publicKey, string privateKey, string collectionName, string emailToken);
}
