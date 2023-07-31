using Bit.Core.Models.Business;

namespace Bit.Core.OrganizationFeatures.DirectoryConnector.Interfaces;

/// <summary>
/// Synchronizes the Users and Groups of an organization by comparing the provided input with the existing database records, adding missing users and groups, and deleting those that are not present in the input.
/// </summary>
public interface IDirectoryConnectorSyncCommand
{
    /// <summary>
    /// Synchronizes organization data asynchronously.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="importingUserId">The optional identifier of the user performing the import.</param>
    /// <param name="groups">The groups to be imported.</param>
    /// <param name="newUsers">The new users to be added to the organization.</param>
    /// <param name="removeUserExternalIds">The external IDs of users to be removed from the organization.</param>
    /// <param name="overwriteExisting">Indicates whether existing users should be overwritten during synchronization.</param>
    Task SyncOrganizationAsync(Guid organizationId, Guid? importingUserId, IEnumerable<ImportedGroup> groups,
        IEnumerable<ImportedOrganizationUser> newUsers, IEnumerable<string> removeUserExternalIds,
        bool overwriteExisting);
}
