using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Microsoft.Extensions.Logging;
using Quartz.Util;

namespace Bit.Core.AdminConsole.Utilities.DebuggingInstruments;

public class UserInviteDebuggingLogger(ILogger<UserInviteDebuggingLogger> logger) : IUserInviteDebuggingLogger
{
    public void Log(OrganizationUser allOrgUser)
    {
        Log([allOrgUser]);
    }

    public void Log(IEnumerable<OrganizationUser> allOrgUsers)
    {
        try
        {
            var invalidInviteState = allOrgUsers.Any(user => user.Status == OrganizationUserStatusType.Invited && user.Email.IsNullOrWhiteSpace());

            if (invalidInviteState)
            {
                var logData = MapObjectDataToLog(allOrgUsers);
                logger.LogWarning("Warning invalid invited state. {logData}", logData);
            }

            var invalidConfirmedOrAcceptedState = allOrgUsers.Any(user => (user.Status == OrganizationUserStatusType.Confirmed || user.Status == OrganizationUserStatusType.Accepted) && !user.Email.IsNullOrWhiteSpace());

            if (invalidConfirmedOrAcceptedState)
            {
                var logData = MapObjectDataToLog(allOrgUsers);
                logger.LogWarning("Warning invalid confirmed or accepted state. {logData}", logData);
            }
        }
        catch (Exception exception)
        {

            // Ensure that this debugging instrument does not interfere with the current flow.
            logger.LogWarning(exception, "Unexpected exception from UserInviteDebuggingLogger");
        }
    }

    private string MapObjectDataToLog(IEnumerable<OrganizationUser> allOrgUsers)
    {
        var log = allOrgUsers.Select(allOrgUser => new
        {
            allOrgUser.OrganizationId,
            allOrgUser.Status,
            hasEmail = !allOrgUser.Email.IsNullOrWhiteSpace(),
            userId = allOrgUser.UserId,
            allOrgUserId = allOrgUser.Id
        });

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        return JsonSerializer.Serialize(log, options);
    }
}
