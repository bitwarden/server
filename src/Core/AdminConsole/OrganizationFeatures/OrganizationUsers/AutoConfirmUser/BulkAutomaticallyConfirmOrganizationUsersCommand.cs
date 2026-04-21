using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public class BulkAutomaticallyConfirmOrganizationUsersCommand(
    IAutomaticallyConfirmOrganizationUserCommand automaticallyConfirmOrganizationUserCommand)
    : IBulkAutomaticallyConfirmOrganizationUsersCommand
{
    public async Task<IReadOnlyList<(Guid OrganizationUserId, string? Error)>> BulkAutomaticallyConfirmOrganizationUsersAsync(
        IEnumerable<AutomaticallyConfirmOrganizationUserRequest> requests)
    {
        var results = new List<(Guid OrganizationUserId, string? Error)>();

        foreach (var request in requests)
        {
            var commandResult = await automaticallyConfirmOrganizationUserCommand
                .AutomaticallyConfirmOrganizationUserAsync(request);

            var errorMessage = commandResult.Match(
                error => error.Message,
                _ => (string?)null);

            results.Add((request.OrganizationUserId, errorMessage));
        }

        return results;
    }
}
