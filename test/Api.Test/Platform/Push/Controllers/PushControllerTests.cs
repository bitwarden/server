#nullable enable
using Bit.Api.Platform.Push;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Bit.Core.NotificationHub;
using Bit.Core.Platform.Push;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Platform.Push.Controllers;

[ControllerCustomize(typeof(PushController))]
[SutProviderCustomize]
public class PushControllerTests
{
    [Theory]
    [BitAutoData(false, true)]
    [BitAutoData(false, false)]
    [BitAutoData(true, true)]
    public async Task RegisterAsync_InstallationIdNotSetOrSelfHosted_BadRequest(bool haveInstallationId,
        bool selfHosted,
        SutProvider<PushController> sutProvider, Guid installationId, Guid userId, Guid identifier, Guid deviceId)
    {
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = selfHosted;
        if (haveInstallationId)
        {
            sutProvider.GetDependency<ICurrentContext>().InstallationId.Returns(installationId);
        }

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.RegisterAsync(new PushRegistrationRequestModel
            {
                DeviceId = deviceId.ToString(),
                PushToken = "test-push-token",
                UserId = userId.ToString(),
                Type = DeviceType.Android,
                Identifier = identifier.ToString(),
            }));

        Assert.Equal("Not correctly configured for push relays.", exception.Message);

        await sutProvider.GetDependency<IPushRegistrationService>().Received(0)
            .CreateOrUpdateRegistrationAsync(Arg.Any<PushRegistrationData>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<DeviceType>(), Arg.Any<IEnumerable<string>>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData(false)]
    [BitAutoData(true)]
    public async Task RegisterAsync_ValidModel_CreatedOrUpdatedRegistration(bool haveOrganizationId,
        SutProvider<PushController> sutProvider, Guid installationId, Guid userId, Guid identifier, Guid deviceId,
        Guid organizationId)
    {
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = false;
        sutProvider.GetDependency<ICurrentContext>().InstallationId.Returns(installationId);

        var expectedUserId = $"{installationId}_{userId}";
        var expectedIdentifier = $"{installationId}_{identifier}";
        var expectedDeviceId = $"{installationId}_{deviceId}";
        var expectedOrganizationId = $"{installationId}_{organizationId}";

        var model = new PushRegistrationRequestModel
        {
            DeviceId = deviceId.ToString(),
            PushToken = "test-push-token",
            UserId = userId.ToString(),
            Type = DeviceType.Android,
            Identifier = identifier.ToString(),
            OrganizationIds = haveOrganizationId ? [organizationId.ToString()] : null,
            InstallationId = installationId
        };

        await sutProvider.Sut.RegisterAsync(model);

        await sutProvider.GetDependency<IPushRegistrationService>().Received(1)
            .CreateOrUpdateRegistrationAsync(
                Arg.Is<PushRegistrationData>(data => data == new PushRegistrationData(model.PushToken)),
                expectedDeviceId, expectedUserId,
                expectedIdentifier, DeviceType.Android, Arg.Do<IEnumerable<string>>(organizationIds =>
                {
                    Assert.NotNull(organizationIds);
                    var organizationIdsList = organizationIds.ToList();
                    if (haveOrganizationId)
                    {
                        Assert.Contains(expectedOrganizationId, organizationIdsList);
                        Assert.Single(organizationIdsList);
                    }
                    else
                    {
                        Assert.Empty(organizationIdsList);
                    }
                }), installationId);
    }
}
