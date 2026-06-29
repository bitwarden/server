using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.StagedUsers;

public class CreateStagedOrganizationUsersCommand(
    IOrganizationUserRepository organizationUserRepository,
    IEventService eventService)
    : ICreateStagedOrganizationUsersCommand
{
    public async Task<CommandResult<ICollection<OrganizationUser>>> RunAsync(
        CreateStagedOrganizationUsersRequest request)
    {
        var requestedUsers = request.Users.ToList();

        var existingEmails = new HashSet<string>(
            await organizationUserRepository.SelectKnownEmailsAsync(
                request.Organization.Id,
                requestedUsers.Select(u => u.Email), false),
            StringComparer.InvariantCultureIgnoreCase);

        var creationDate = DateTime.UtcNow;

        var organizationUsersToCreate = new List<OrganizationUser>();
        foreach (var user in requestedUsers)
        {
            // existingEmails doubles as the running "seen" set, so duplicate emails within this batch
            // are skipped alongside emails already present in the organization.
            if (!existingEmails.Add(user.Email))
            {
                continue;
            }

            organizationUsersToCreate.Add(new OrganizationUser
            {
                OrganizationId = request.Organization.Id,
                UserId = null,
                Email = user.Email.ToLowerInvariant(),
                Key = null,
                Type = OrganizationUserType.User,
                Status = OrganizationUserStatusType.Staged,
                // StatusNew is intentionally left null - it is only populated by the revoke flow
                ExternalId = user.ExternalId,
                CreationDate = creationDate,
                RevisionDate = creationDate,
            });
        }

        if (organizationUsersToCreate.Count == 0)
        {
            return organizationUsersToCreate;
        }

        await organizationUserRepository.CreateManyAsync(organizationUsersToCreate);

        await eventService.LogOrganizationUserEventsAsync(
            organizationUsersToCreate.Select(organizationUser =>
                (organizationUser, EventType.OrganizationUser_Staged, request.EventSystemUser, (DateTime?)creationDate)));

        return organizationUsersToCreate;
    }
}
