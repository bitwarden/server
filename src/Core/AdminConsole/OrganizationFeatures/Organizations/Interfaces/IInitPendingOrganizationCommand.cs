using Bit.Core.Entities;
namespace Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IInitPendingOrganizationCommand
{
    /// <summary>
    /// Update an Organization entry by setting the public/private keys, set it as 'Enabled' and move the Status from 'Pending' to 'Created'.
    /// </summary>
    /// <remarks>
    /// This method must target a disabled Organization that has null keys and status as 'Pending'.
    /// </remarks>
    Task InitPendingOrganizationAsync(User user, Guid organizationId, Guid organizationUserId, string publicKey, string privateKey, string collectionName, string emailToken);
}
