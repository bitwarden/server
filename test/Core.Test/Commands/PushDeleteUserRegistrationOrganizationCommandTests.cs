using Bit.Core.Commands;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Commands.Users;

[SutProviderCustomize]
public class PushDeleteUserRegistrationOrganizationCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteAndPushUserRegistration_Success(SutProvider<PushDeleteUserRegistrationOrganizationCommand> sutProvider, Guid organizationId, Guid userId, ICollection<Device> devices)
    {
        sutProvider.GetDependency<IDeviceRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(devices);

        await sutProvider.Sut.DeleteAndPushUserRegistrationAsync(organizationId, userId);

        await sutProvider.GetDependency<IDeviceRepository>().Received(1).GetManyByUserIdAsync(userId);
        await sutProvider.GetDependency<IPushRegistrationService>().Received(1).DeleteUserRegistrationOrganizationAsync(Arg.Any<IEnumerable<string>>(), organizationId.ToString());
        await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushSyncOrgKeysAsync(userId);
    }
}
