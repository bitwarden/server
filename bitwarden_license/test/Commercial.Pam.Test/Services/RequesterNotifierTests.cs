using Bit.Commercial.Pam.Services;
using Bit.Core.Platform.Push;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Pam.Test.Services;

[SutProviderCustomize]
public class RequesterNotifierTests
{
    [Theory, BitAutoData]
    public async Task NotifyRequesterAsync_PushesToRequester(
        SutProvider<RequesterNotifier> sutProvider, Guid requesterId)
    {
        await sutProvider.Sut.NotifyRequesterAsync(requesterId);

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushRefreshAccessRequestAsync(requesterId);
    }
}
