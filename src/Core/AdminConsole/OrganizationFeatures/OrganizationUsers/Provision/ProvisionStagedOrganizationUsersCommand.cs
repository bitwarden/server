using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Provision;

public class ProvisionStagedOrganizationUsersCommand(
    IOrganizationUserRepository organizationUserRepository,
    IEventService eventService)
    : IProvisionStagedOrganizationUsersCommand
{
    public async Task<ICollection<OrganizationUser>> ProvisionStagedOrganizationUsersAsync(
        Organization organization,
        IEnumerable<(string Email, string ExternalId)> users,
        EventSystemUser eventSystemUser)
    {
        var requestedUsers = users.ToList();

        var existingEmails = new HashSet<string>(
            await organizationUserRepository.SelectKnownEmailsAsync(
                organization.Id, requestedUsers.Select(u => u.Email), false),
            StringComparer.InvariantCultureIgnoreCase);

        var creationDate = DateTime.UtcNow;

        var organizationUsersToCreate = new List<OrganizationUser>();
        foreach (var (email, externalId) in requestedUsers)
        {
            if (!existingEmails.Add(email))
            {
                continue;
            }

            organizationUsersToCreate.Add(new OrganizationUser
            {
                OrganizationId = organization.Id,
                UserId = null,
                Email = email.ToLowerInvariant(),
                Key = null,
                Type = OrganizationUserType.User,
                Status = OrganizationUserStatusType.Staged,
                // StatusNew is intentionally left null - it is only populated by the revoke flow when
                ExternalId = externalId,
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
                (organizationUser, EventType.OrganizationUser_Staged, eventSystemUser, (DateTime?)creationDate)));

        return organizationUsersToCreate;
    }
}
