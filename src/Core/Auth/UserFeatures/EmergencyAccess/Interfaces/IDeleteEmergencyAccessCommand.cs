using Bit.Core.Exceptions;

namespace Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;

/// <summary>
/// Command for deleting emergency access records.
/// </summary>
public interface IDeleteEmergencyAccessCommand
{
    /// <summary>
    /// Deletes a single emergency access record if the requesting user is the grantor or grantee.
    /// Sends an email notification to the grantor when a grantee is removed.
    /// </summary>
    /// <param name="emergencyAccessId">The ID of the emergency access record to delete.</param>
    /// <param name="userId">The ID of the requesting user; must be either the grantor or grantee of the record.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="BadRequestException">
    /// Thrown when the emergency access record is not found or does not belong to the specified user.
    /// </exception>
    Task DeleteByIdAndUserIdAsync(Guid emergencyAccessId, Guid userId);

    /// <summary>
    /// Deletes all emergency access records where the user IDs are either grantors or grantees.
    /// Sends email notifications only to grantors when their grantees are removed.
    /// </summary>
    /// <param name="userIds">The IDs of users whose emergency access records — as grantor or grantee — will be deleted.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// If no records are found for the provided user IDs, the method returns.
    /// </remarks>
    Task DeleteAllByUserIdsAsync(ICollection<Guid> userIds);

    /// <summary>
    /// Deletes all emergency access records where the user ID is either a grantor or a grantee.
    /// Sends email notifications only to grantors when their grantees are removed.
    /// </summary>
    /// <param name="userId">The ID of the user whose emergency access records — as grantor or grantee — will be deleted.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// If no records are found for the provided user ID, the method returns.
    /// </remarks>
    Task DeleteAllByUserIdAsync(Guid userId);
}
