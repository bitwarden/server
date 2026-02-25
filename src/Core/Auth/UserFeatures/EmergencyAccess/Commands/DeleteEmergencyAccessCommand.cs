using Bit.Core.Auth.UserFeatures.EmergencyAccess.Interfaces;
using Bit.Core.Auth.UserFeatures.EmergencyAccess.Mail;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Mail.Mailer;
using Bit.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Auth.UserFeatures.EmergencyAccess.Commands;

public class DeleteEmergencyAccessCommand(
    IEmergencyAccessRepository _emergencyAccessRepository,
    IMailer mailer,
    ILogger<DeleteEmergencyAccessCommand> _logger) : IDeleteEmergencyAccessCommand
{
    /// <inheritdoc />
    public async Task DeleteByIdAndUserIdAsync(Guid emergencyAccessId, Guid userId)
    {
        var emergencyAccessDetails = await _emergencyAccessRepository.GetDetailsByIdAsync(emergencyAccessId);

        // Error if the emergency access doesn't exist or the user trying to delete is neither the grantor nor the grantee
        if (emergencyAccessDetails == null || (emergencyAccessDetails.GrantorId != userId && emergencyAccessDetails.GranteeId != userId))
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        await _emergencyAccessRepository.DeleteAsync(emergencyAccessDetails);

        // Emails may be null if the grantor or grantee user account has since been deleted
        // so ensure we have both emails we need.
        if (!string.IsNullOrEmpty(emergencyAccessDetails.GrantorEmail) &&
            !string.IsNullOrEmpty(emergencyAccessDetails.GranteeEmail))
        {
            await SendGranteesRemovalNotificationToGrantorAsync(
                emergencyAccessDetails.GrantorEmail,
                [emergencyAccessDetails.GranteeEmail]);
        }
        else
        {
            // If we are missing the emails needed to send a notification, log this occurrence.
            _logger.LogWarning(
                "Emergency access deletion notification skipped for grantor {GrantorId} and grantee {GranteeId}: GrantorEmail missing: {GrantorEmailMissing}, GranteeEmail missing: {GranteeEmailMissing}.",
                emergencyAccessDetails.GrantorId,
                emergencyAccessDetails.GranteeId,
                string.IsNullOrEmpty(emergencyAccessDetails.GrantorEmail),
                string.IsNullOrEmpty(emergencyAccessDetails.GranteeEmail));
        }
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

        // After deletion, send notifications to grantors about their removed grantees.
        // GrantorEmail may be null when a grantor's account has been deleted, since it is sourced
        // entirely from a LEFT JOIN on the User table with no fallback column. Log any affected
        // GrantorIds up front for traceability — the grantor's account is already gone so the ID
        // cannot be used to look up the user, but it can be correlated with audit logs generated
        // at the time of that account's deletion to understand why the notification was skipped.
        var grantorIdsWithNullEmail = emergencyAccessDetails
            .Where(ea => string.IsNullOrEmpty(ea.GrantorEmail))
            .Select(ea => ea.GrantorId)
            .Distinct()
            .ToList();

        if (grantorIdsWithNullEmail.Count > 0)
        {
            _logger.LogWarning(
                "Emergency access deletion notification skipped for {Count} grantor(s) with missing GrantorEmail. GrantorIds: {GrantorIds}.",
                grantorIdsWithNullEmail.Count,
                grantorIdsWithNullEmail);
        }

        // Group by grantor email to send each grantor a single email listing all their removed grantees.
        // Records with null GrantorEmail are excluded above and will not receive a notification.
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

            var granteeIdsWithNullEmail = grantorGroup
                .Where(ea => string.IsNullOrEmpty(ea.GranteeEmail))
                .Select(ea => ea.GranteeId)
                .Distinct()
                .ToList();

            if (granteeIdsWithNullEmail.Count > 0)
            {
                _logger.LogWarning(
                    "Emergency access deletion notification skipped for {Count} grantee(s) with missing GranteeEmail. GranteeIds: {GranteeIds}.",
                    granteeIdsWithNullEmail.Count,
                    granteeIdsWithNullEmail);
            }

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
