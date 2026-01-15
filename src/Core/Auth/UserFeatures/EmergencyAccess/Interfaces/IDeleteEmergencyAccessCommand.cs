using Bit.Core.Auth.Models.Data;
using Bit.Core.Exceptions;

namespace Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;

/// <summary>
/// Command for deleting emergency access records based on the grantor's user ID.
/// </summary>
public interface IDeleteEmergencyAccessCommand
{
    /// <summary>
    /// Deletes a single emergency access record for the specified grantor.
    /// </summary>
    /// <param name="emergencyAccessId">The ID of the emergency access record to delete.</param>
    /// <param name="grantorId">The ID of the grantor user who owns the emergency access record.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="BadRequestException">
    /// Thrown when the emergency access record is not found or does not belong to the specified grantor.
    /// </exception>
    Task<EmergencyAccessDetails> DeleteByIdGrantorIdAsync(Guid emergencyAccessId, Guid grantorId);

    /// <summary>
    /// Deletes all emergency access records for the specified grantor.
    /// </summary>
    /// <param name="grantorId">The ID of the grantor user whose emergency access records should be deleted.</param>
    /// <returns>A collection of the deleted emergency access records.</returns>
    Task<ICollection<EmergencyAccessDetails>> DeleteAllByGrantorIdAsync(Guid grantorId);
}
