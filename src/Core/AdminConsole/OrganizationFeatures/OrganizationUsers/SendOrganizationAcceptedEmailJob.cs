using Bit.Core.Enums;
using Bit.Core.Jobs;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;

/// <summary>
/// Sends the accepted email to organization admins, but only if the user is still awaiting confirmation.
/// </summary>
public class SendOrganizationAcceptedEmailJob(
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationRepository organizationRepository,
    IUserRepository userRepository,
    IMailService mailService,
    ILogger<SendOrganizationAcceptedEmailJob> logger)
    : BaseJob(logger)
{
    public const string OrganizationUserIdKey = "organizationUserId";

    protected override async Task ExecuteJobAsync(IJobExecutionContext context)
    {
        var organizationUserId = Guid.Parse(context.MergedJobDataMap.GetString(OrganizationUserIdKey)!);

        var organizationUser = await organizationUserRepository.GetByIdAsync(organizationUserId);
        if (organizationUser is not { Status: OrganizationUserStatusType.Accepted, UserId: not null })
        {
            return;
        }

        var organization = await organizationRepository.GetByIdAsync(organizationUser.OrganizationId);
        var user = await userRepository.GetByIdAsync(organizationUser.UserId.Value);
        if (organization is null || user is null)
        {
            return;
        }

        var admins = await organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin);
        var adminEmails = admins.Select(a => a.Email).Distinct().ToList();
        if (adminEmails.Count > 0)
        {
            await mailService.SendOrganizationAcceptedEmailAsync(organization, user.Email, adminEmails);
        }
    }
}
