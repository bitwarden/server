using Bit.Core.Auth.Models.Data;
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
    public async Task<EmergencyAccessDetails> DeleteByIdGrantorIdAsync(Guid emergencyAccessId, Guid grantorId)
    {
        var emergencyAccessDetails = await _emergencyAccessRepository.GetDetailsByIdGrantorIdAsync(emergencyAccessId, grantorId);

        if (emergencyAccessDetails == null || emergencyAccessDetails.GrantorId != grantorId)
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        var (grantorEmails, granteeEmails) = await DeleteEmergencyAccessAsync([emergencyAccessDetails]);

        // Send notification email to grantor
        await SendEmergencyAccessRemoveGranteesEmailAsync(grantorEmails, granteeEmails);
        return emergencyAccessDetails;
    }

    /// <inheritdoc />
    public async Task<ICollection<EmergencyAccessDetails>?> DeleteAllByGrantorIdAsync(Guid grantorId)
    {
        var emergencyAccessDetails = await _emergencyAccessRepository.GetManyDetailsByGrantorIdAsync(grantorId);

        // if there is nothing return an empty array and do not send an email
        if (emergencyAccessDetails.Count == 0)
        {
            return emergencyAccessDetails;
        }

        var (grantorEmails, granteeEmails) = await DeleteEmergencyAccessAsync(emergencyAccessDetails);

        // Send notification email to grantor
        await SendEmergencyAccessRemoveGranteesEmailAsync(grantorEmails, granteeEmails);

        return emergencyAccessDetails;
    }

    /// <inheritdoc />
    public async Task<ICollection<EmergencyAccessDetails>?> DeleteAllByGranteeIdAsync(Guid granteeId)
    {
        var emergencyAccessDetails = await _emergencyAccessRepository.GetManyDetailsByGranteeIdAsync(granteeId);

        // if there is nothing return an empty array
        if (emergencyAccessDetails == null || emergencyAccessDetails.Count == 0)
        {
            return emergencyAccessDetails;
        }

        var (grantorEmails, granteeEmails) = await DeleteEmergencyAccessAsync(emergencyAccessDetails);

        // Send notification email to grantor(s)
        await SendEmergencyAccessRemoveGranteesEmailAsync(grantorEmails, granteeEmails);

        return emergencyAccessDetails;
    }

    private async Task<(HashSet<string> grantorEmails, HashSet<string> granteeEmails)> DeleteEmergencyAccessAsync(IEnumerable<EmergencyAccessDetails> emergencyAccessDetails)
    {
        var grantorEmails = new HashSet<string>();
        var granteeEmails = new HashSet<string>();

        await _emergencyAccessRepository.DeleteManyAsync([.. emergencyAccessDetails.Select(ea => ea.Id)]);

        foreach (var details in emergencyAccessDetails)
        {
            granteeEmails.Add(details.GranteeEmail ?? string.Empty);
            grantorEmails.Add(details.GrantorEmail);
        }

        return (grantorEmails, granteeEmails);
    }

    /// <summary>
    /// Sends an email notification to the grantor about removed grantees.
    /// </summary>
    /// <param name="grantorEmails">The email addresses of the grantors to notify when deleting by grantee</param>
    /// <param name="formattedGranteeIdentifiers">The formatted identifiers of the removed grantees to include in the email</param>
    /// <returns></returns>
    private async Task SendEmergencyAccessRemoveGranteesEmailAsync(IEnumerable<string> grantorEmails, IEnumerable<string> formattedGranteeIdentifiers)
    {
        foreach (var email in grantorEmails)
        {
            var emailViewModel = new EmergencyAccessRemoveGranteesMail
            {
                ToEmails = [email],
                View = new EmergencyAccessRemoveGranteesMailView
                {
                    RemovedGranteeEmails = formattedGranteeIdentifiers
                }
            };

            await mailer.SendEmail(emailViewModel);
        }
    }
}
