using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Provision;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.Provision;

[SutProviderCustomize]
public class ProvisionStagedOrganizationUsersCommandTests
{
    [Theory, BitAutoData]
    public async Task ProvisionStagedOrganizationUsersAsync_CreatesStagedRows_WithExpectedFields(
        Organization organization,
        SutProvider<ProvisionStagedOrganizationUsersCommand> sutProvider)
    {
        var users = new[] { ("user1@example.com", "ext-1"), ("USER2@example.com", "ext-2") };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .SelectKnownEmailsAsync(organization.Id, Arg.Any<IEnumerable<string>>(), false)
            .Returns(new List<string>());

        var result = await sutProvider.Sut.ProvisionStagedOrganizationUsersAsync(
            organization, users, EventSystemUser.SCIM);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .CreateManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(created =>
                created.Count() == 2 &&
                created.All(ou =>
                    ou.OrganizationId == organization.Id &&
                    ou.Status == OrganizationUserStatusType.Staged &&
                    ou.Type == OrganizationUserType.User &&
                    ou.UserId == null &&
                    ou.Key == null &&
                    ou.StatusNew == null &&
                    !string.IsNullOrEmpty(ou.Email) &&
                    !string.IsNullOrEmpty(ou.ExternalId)) &&
                created.Any(ou => ou.Email == "user1@example.com" && ou.ExternalId == "ext-1") &&
                // Email is normalized to lower-case.
                created.Any(ou => ou.Email == "user2@example.com" && ou.ExternalId == "ext-2")));

        Assert.Equal(2, result.Count);
    }

    [Theory, BitAutoData]
    public async Task ProvisionStagedOrganizationUsersAsync_EmitsStagedEvent_NotInvitedEvent(
        Organization organization,
        SutProvider<ProvisionStagedOrganizationUsersCommand> sutProvider)
    {
        var users = new[] { ("user@example.com", "ext-1") };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .SelectKnownEmailsAsync(organization.Id, Arg.Any<IEnumerable<string>>(), false)
            .Returns(new List<string>());

        await sutProvider.Sut.ProvisionStagedOrganizationUsersAsync(
            organization, users, EventSystemUser.SCIM);

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventsAsync(
                Arg.Is<IEnumerable<(OrganizationUser, EventType, EventSystemUser, DateTime?)>>(events =>
                    events.Count() == 1 &&
                    events.All(e => e.Item2 == EventType.OrganizationUser_Staged &&
                                    e.Item3 == EventSystemUser.SCIM)));

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceive()
            .LogOrganizationUserEventsAsync(
                Arg.Is<IEnumerable<(OrganizationUser, EventType, EventSystemUser, DateTime?)>>(events =>
                    events.Any(e => e.Item2 == EventType.OrganizationUser_Invited)));
    }

    [Theory, BitAutoData]
    public async Task ProvisionStagedOrganizationUsersAsync_DoesNotPerformSeatCheck(
        Organization organization,
        SutProvider<ProvisionStagedOrganizationUsersCommand> sutProvider)
    {
        var users = new[] { ("user@example.com", "ext-1") };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .SelectKnownEmailsAsync(organization.Id, Arg.Any<IEnumerable<string>>(), false)
            .Returns(new List<string>());

        await sutProvider.Sut.ProvisionStagedOrganizationUsersAsync(
            organization, users, EventSystemUser.SCIM);

        // Staged users do not consume a seat, so no occupied-seat lookup should occur. The command also
        // does not depend on IMailService or any autoscale command, so the absence of invite emails and
        // seat autoscale is guaranteed by construction.
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceive()
            .GetOccupiedSmSeatCountByOrganizationIdAsync(Arg.Any<Guid>());
    }

    [Theory, BitAutoData]
    public async Task ProvisionStagedOrganizationUsersAsync_SkipsEmailsAlreadyInOrganization(
        Organization organization,
        SutProvider<ProvisionStagedOrganizationUsersCommand> sutProvider)
    {
        var users = new[]
        {
            ("existing@example.com", "ext-existing"),
            ("new@example.com", "ext-new"),
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .SelectKnownEmailsAsync(organization.Id, Arg.Any<IEnumerable<string>>(), false)
            .Returns(new List<string> { "existing@example.com" });

        var result = await sutProvider.Sut.ProvisionStagedOrganizationUsersAsync(
            organization, users, EventSystemUser.SCIM);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .CreateManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(created =>
                created.Count() == 1 && created.Single().Email == "new@example.com"));

        Assert.Single(result);
    }

    [Theory, BitAutoData]
    public async Task ProvisionStagedOrganizationUsersAsync_DeduplicatesEmailsWithinBatch(
        Organization organization,
        SutProvider<ProvisionStagedOrganizationUsersCommand> sutProvider)
    {
        var users = new[]
        {
            ("dup@example.com", "ext-1"),
            ("DUP@example.com", "ext-2"),
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .SelectKnownEmailsAsync(organization.Id, Arg.Any<IEnumerable<string>>(), false)
            .Returns(new List<string>());

        var result = await sutProvider.Sut.ProvisionStagedOrganizationUsersAsync(
            organization, users, EventSystemUser.SCIM);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .CreateManyAsync(Arg.Is<IEnumerable<OrganizationUser>>(created => created.Count() == 1));

        Assert.Single(result);
    }

    [Theory, BitAutoData]
    public async Task ProvisionStagedOrganizationUsersAsync_WhenAllEmailsExist_CreatesNothingAndLogsNoEvents(
        Organization organization,
        SutProvider<ProvisionStagedOrganizationUsersCommand> sutProvider)
    {
        var users = new[] { ("existing@example.com", "ext-1") };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .SelectKnownEmailsAsync(organization.Id, Arg.Any<IEnumerable<string>>(), false)
            .Returns(new List<string> { "existing@example.com" });

        var result = await sutProvider.Sut.ProvisionStagedOrganizationUsersAsync(
            organization, users, EventSystemUser.SCIM);

        Assert.Empty(result);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceive()
            .CreateManyAsync(Arg.Any<IEnumerable<OrganizationUser>>());
        await sutProvider.GetDependency<IEventService>()
            .DidNotReceive()
            .LogOrganizationUserEventsAsync(
                Arg.Any<IEnumerable<(OrganizationUser, EventType, EventSystemUser, DateTime?)>>());
    }
}
