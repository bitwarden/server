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
        var emergencyAccess = await _emergencyAccessRepository.GetDetailsByIdGrantorIdAsync(emergencyAccessId, grantorId);

        if (emergencyAccess == null || emergencyAccess.GrantorId != grantorId)
        {
            throw new BadRequestException("Emergency Access not valid.");
        }

        await _emergencyAccessRepository.DeleteAsync(emergencyAccess);

        // Send notification email to grantor
        await SendEmergencyAccessRemoveGranteesEmailAsync(emergencyAccess.GrantorEmail, [emergencyAccess.GranteeName]);
        return emergencyAccess;
    }

    /// <inheritdoc />
    public async Task<ICollection<EmergencyAccessDetails>?> DeleteAllByGrantorIdAsync(Guid grantorId)
    {
        var emergencyAccessDetails = await _emergencyAccessRepository.GetManyDetailsByGrantorIdAsync(grantorId);

        // if there is nothing return an empty array
        if (emergencyAccessDetails == null || emergencyAccessDetails.Count == 0)
        {
            return emergencyAccessDetails;
        }

        foreach (var details in emergencyAccessDetails)
        {
            var emergencyAccess = details.ToEmergencyAccess();
            await _emergencyAccessRepository.DeleteAsync(emergencyAccess);
        }

        // Send notification email to grantor
        await SendEmergencyAccessRemoveGranteesEmailAsync(
            emergencyAccessDetails.FirstOrDefault()?.GrantorEmail ?? string.Empty,
            [.. emergencyAccessDetails.Select(e => e.GranteeName)]);

        return emergencyAccessDetails;
    }

    private async Task SendEmergencyAccessRemoveGranteesEmailAsync(string grantorEmail, string[] granteeNames)
    {
        var email = new EmergencyAccessRemoveGranteesMail
        {
            ToEmails = [grantorEmail],
            View = new EmergencyAccessRemoveGranteesMailView
            {
                RemovedGranteeNames = granteeNames
            }
        };

        await mailer.SendEmail(email);
    }
}
