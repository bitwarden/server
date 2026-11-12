using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

public class SendOrganizationAcceptedEmailCommand(
    IOrganizationUserRepository organizationUserRepository,
    IMailService mailService)
    : ISendOrganizationAcceptedEmailCommand
{
    public async Task SendAsync(Organization organization, string acceptedUserEmail)
    {
        var admins = await organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin);
        var adminEmails = admins.Select(a => a.Email).Distinct().ToList();
        if (adminEmails.Count > 0)
        {
            await mailService.SendOrganizationAcceptedEmailAsync(organization, acceptedUserEmail, adminEmails);
        }
    }
}
