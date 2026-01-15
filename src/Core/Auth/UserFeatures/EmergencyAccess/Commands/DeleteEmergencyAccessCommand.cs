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
        await SendEmailAsync(emergencyAccess.GrantorEmail, [emergencyAccess.GranteeName]);
        return emergencyAccess;
    }

    /// <inheritdoc />
    public async Task<ICollection<EmergencyAccessDetails>> DeleteAllByGrantorIdAsync(Guid grantorId)
    {
        var emergencyAccessDetails = await _emergencyAccessRepository.GetManyDetailsByGrantorIdAsync(grantorId);

        foreach (var details in emergencyAccessDetails)
        {
            var emergencyAccess = details.ToEmergencyAccess();
            await _emergencyAccessRepository.DeleteAsync(emergencyAccess);
        }

        // Send notification email to grantor
        await SendEmailAsync(
            emergencyAccessDetails.FirstOrDefault()?.GrantorEmail ?? string.Empty,
            [.. emergencyAccessDetails.Select(e => e.GranteeName)]);

        return emergencyAccessDetails;
    }

    private async Task SendEmailAsync(string grantorEmail, string[] granteeNames)
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
