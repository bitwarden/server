using Bit.Core.AdminConsole.Entities;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationConfirmation;

public class SendOrganizationConfirmationCommand(IMailService mailService)
    : ISendOrganizationConfirmationCommand
{
    public async Task SendConfirmationAsync(Organization organization, string userEmail, bool accessSecretsManager = false)
    {
        await mailService.SendUpdatedOrganizationConfirmedEmailAsync(organization, userEmail, accessSecretsManager);
    }
}
