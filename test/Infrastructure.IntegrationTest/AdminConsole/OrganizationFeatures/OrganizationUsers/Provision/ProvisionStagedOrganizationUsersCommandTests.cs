using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Provision;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.OrganizationFeatures.OrganizationUsers.Provision;

public class ProvisionStagedOrganizationUsersCommandTests
{
    [DatabaseTheory, DatabaseData]
    public async Task ProvisionStagedOrganizationUsersAsync_CreatesStagedMembers(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        // The command is a thin orchestration over the repository; event emission is covered by unit tests,
        // so a no-op event service keeps this integration test focused on real database persistence.
        var sut = new ProvisionStagedOrganizationUsersCommand(organizationUserRepository, new NoopEventService());

        var users = new[]
        {
            (Email: $"Staged.One-{Guid.NewGuid()}@Example.com", ExternalId: $"ext-{Guid.NewGuid()}"),
            (Email: $"Staged.Two-{Guid.NewGuid()}@Example.com", ExternalId: $"ext-{Guid.NewGuid()}"),
        };

        var result = await sut.ProvisionStagedOrganizationUsersAsync(organization, users, EventSystemUser.SCIM);

        // The command reports both created members, each with a generated id.
        Assert.Equal(2, result.Count);
        Assert.All(result, organizationUser => Assert.NotEqual(Guid.Empty, organizationUser.Id));

        // Emails are persisted normalized to lower-case and external ids are preserved verbatim.
        Assert.Equal(
            users.Select(u => u.Email.ToLowerInvariant()).OrderBy(email => email),
            result.Select(organizationUser => organizationUser.Email).OrderBy(email => email));
        Assert.Equal(
            users.Select(u => u.ExternalId).OrderBy(externalId => externalId),
            result.Select(organizationUser => organizationUser.ExternalId).OrderBy(externalId => externalId));

        // Each row is actually persisted as a Staged member with the expected shape.
        foreach (var created in result)
        {
            var persisted = await organizationUserRepository.GetByIdAsync(created.Id);

            Assert.NotNull(persisted);
            Assert.Equal(organization.Id, persisted.OrganizationId);
            Assert.Equal(OrganizationUserStatusType.Staged, persisted.Status);
            Assert.Equal(OrganizationUserType.User, persisted.Type);
            Assert.Equal(created.Email, persisted.Email);
            Assert.Equal(created.ExternalId, persisted.ExternalId);
            Assert.Null(persisted.UserId);
            Assert.Null(persisted.Key);
            Assert.Null(persisted.StatusNew);
        }
    }
}
