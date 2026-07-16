using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Queries;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Commands;

[SutProviderCustomize]
public class CreateManyTaskNotificationsCommandTest
{
    [Theory]
    [BitAutoData]
    public async Task CreateAsync_VFO1FoundationDisabled_UsesV1Template(
        SutProvider<CreateManyTaskNotificationsCommand> sutProvider,
        Organization organization,
        ICollection<SecurityTask> securityTasks,
        UserSecurityTaskCipher userSecurityTaskCipher)
    {
        userSecurityTaskCipher.UserId = Guid.NewGuid();
        Setup(sutProvider, organization, userSecurityTaskCipher, vfo1FoundationEnabled: false);

        await sutProvider.Sut.CreateAsync(organization.Id, securityTasks);

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendBulkSecurityTaskNotificationsAsync(
                organization,
                Arg.Any<IEnumerable<UserSecurityTasksCount>>(),
                Arg.Any<IEnumerable<string>>(),
                false);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_VFO1FoundationEnabled_UsesV2Template(
        SutProvider<CreateManyTaskNotificationsCommand> sutProvider,
        Organization organization,
        ICollection<SecurityTask> securityTasks,
        UserSecurityTaskCipher userSecurityTaskCipher)
    {
        userSecurityTaskCipher.UserId = Guid.NewGuid();
        Setup(sutProvider, organization, userSecurityTaskCipher, vfo1FoundationEnabled: true);

        await sutProvider.Sut.CreateAsync(organization.Id, securityTasks);

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendBulkSecurityTaskNotificationsAsync(
                organization,
                Arg.Any<IEnumerable<UserSecurityTasksCount>>(),
                Arg.Any<IEnumerable<string>>(),
                true);
    }

    private static void Setup(
        SutProvider<CreateManyTaskNotificationsCommand> sutProvider,
        Organization organization,
        UserSecurityTaskCipher userSecurityTaskCipher,
        bool vfo1FoundationEnabled)
    {
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.VFO1Foundation)
            .Returns(vfo1FoundationEnabled);
        sutProvider.GetDependency<IGetSecurityTasksNotificationDetailsQuery>()
            .GetNotificationDetailsByManyIds(organization.Id, Arg.Any<IEnumerable<SecurityTask>>())
            .Returns(new List<UserSecurityTaskCipher> { userSecurityTaskCipher });
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);
    }
}
