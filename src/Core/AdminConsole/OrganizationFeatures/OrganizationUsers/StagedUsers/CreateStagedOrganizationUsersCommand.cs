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

        var organizationUsersToCreate = requestedUsers
            .Where(w => !existingEmails.Add(w.Email))
            .Select(s => new OrganizationUser
            {
                OrganizationId = request.Organization.Id,
                UserId = null,
                Email = s.Email.ToLowerInvariant(),
                Key = null,
                Type = OrganizationUserType.User,
                Status = OrganizationUserStatusType.Staged,
                // StatusNew is intentionally left null - it is only populated by the revoke flow
                ExternalId = s.ExternalId,
                CreationDate = creationDate,
                RevisionDate = creationDate,
            }).ToList();

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
