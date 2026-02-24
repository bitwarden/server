using Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Mail;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.EmergencyAccess.Commands;

public class DeleteEmergencyAccessCommand(
    IEmergencyAccessRepository _emergencyAccessRepository,
    IMailer mailer) : IDeleteEmergencyAccessCommand
{
    /// <inheritdoc />
    public async Task DeleteByIdAndUserIdAsync(Guid emergencyAccessId, Guid userId)
    {
        var emergencyAccess = await _emergencyAccessRepository.GetByIdAsync(emergencyAccessId);

        // Error if the emergency access doesn't exist or the user trying to delete is neither the grantor nor the grantee
        if (emergencyAccess == null || (emergencyAccess.GrantorId != userId && emergencyAccess.GranteeId != userId))
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        await _emergencyAccessRepository.DeleteAsync(emergencyAccess);


        // TODO: add email notification support 
        // Emails may be null if the grantor or grantee user account has since been deleted
        // so ensure we have both emails we need.
        // if (!string.IsNullOrEmpty(emergencyAccessDetails.GrantorEmail) &&
        //     !string.IsNullOrEmpty(emergencyAccessDetails.GranteeEmail))
        // {
        //     await SendGranteesRemovalNotificationToGrantorAsync(
        //         emergencyAccessDetails.GrantorEmail,
        //         [emergencyAccessDetails.GranteeEmail]);
        // }
    }

    /// <inheritdoc />
    public async Task DeleteAllByUserIdAsync(Guid userId)
    {
        await DeleteAllByUserIdsAsync([userId]);
    }


    /// <inheritdoc />
    public async Task DeleteAllByUserIdsAsync(ICollection<Guid> userIds)
    {
        var emergencyAccessDetails = await _emergencyAccessRepository.GetManyDetailsByUserIdsAsync(userIds);

        if (emergencyAccessDetails.Count == 0)
        {
            // No records found, so nothing to delete or notify
            // However, don't throw an error since the end state of "no records for these user IDs" 
            // is already achieved
            return;
        }

        // Delete all records using existing DeleteManyAsync (batching already implemented)
        var emergencyAccessIds = emergencyAccessDetails.Select(ea => ea.Id).ToList();
        await _emergencyAccessRepository.DeleteManyAsync(emergencyAccessIds);

        // Email notifications: Notify all affected grantors about their removed grantees
        // Group by grantor to send each grantor only their specific removed grantees
        // GrantorEmail may be null if the grantor's account has since been deleted
        // so we must filter out null GrantorEmails before grouping and sending notifications.
        var grantorEmergencyAccessDetailGroups = emergencyAccessDetails
            .Where(ea => !string.IsNullOrEmpty(ea.GrantorEmail))
            .GroupBy(ea => ea.GrantorEmail!); // .GrantorEmail is safe here due to the Where above

        foreach (var grantorGroup in grantorEmergencyAccessDetailGroups)
        {
            var grantorEmail = grantorGroup.Key;
            var granteeEmails = grantorGroup
                .Select(ea => ea.GranteeEmail)
                // Filter out null grantee emails, which may occur if a grantee's account has been deleted
                .Where(e => !string.IsNullOrEmpty(e))
                .Cast<string>() // Cast is safe here due to the Where above
                .Distinct();

            if (granteeEmails.Any())
            {
                await SendGranteesRemovalNotificationToGrantorAsync(grantorEmail, granteeEmails);
            }
        }
    }

    /// <summary>
    /// Sends an email notification to a grantor about their removed grantees.
    /// </summary>
    /// <param name="grantorEmail">The email address of the grantor to notify</param>
    /// <param name="granteeEmails">The email addresses of the removed grantees</param>
    private async Task SendGranteesRemovalNotificationToGrantorAsync(string grantorEmail, IEnumerable<string> granteeEmails)
    {
        var emailViewModel = new EmergencyAccessRemoveGranteesMail
        {
            ToEmails = [grantorEmail],
            View = new EmergencyAccessRemoveGranteesMailView
            {
                RemovedGranteeEmails = granteeEmails
            }
        };

        await mailer.SendEmail(emailViewModel);
    }
}
