#nullable enable
using Bit.Api.Controllers;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers;

[ControllerCustomize(typeof(PushController))]
[SutProviderCustomize]
public class PushControllerTests
{
    [Theory]
    [BitAutoData(false, true)]
    [BitAutoData(false, false)]
    [BitAutoData(true, true)]
    public async Task SendAsync_InstallationIdNotSetOrSelfHosted_BadRequest(bool haveInstallationId, bool selfHosted,
        SutProvider<PushController> sutProvider, Guid installationId, Guid userId, Guid organizationId)
    {
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = selfHosted;
        if (haveInstallationId)
        {
            sutProvider.GetDependency<ICurrentContext>().InstallationId.Returns(installationId);
        }

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SendAsync(new PushSendRequestModel
            {
                Type = PushType.SyncNotification,
                UserId = userId.ToString(),
                OrganizationId = organizationId.ToString(),
                InstallationId = installationId.ToString(),
                Payload = "test-payload"
            }));

        Assert.Equal("Not correctly configured for push relays.", exception.Message);

        await sutProvider.GetDependency<IPushNotificationService>().Received(0)
            .SendPayloadToUserAsync(Arg.Any<string>(), Arg.Any<PushType>(), Arg.Any<object>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<ClientType?>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(0)
            .SendPayloadToOrganizationAsync(Arg.Any<string>(), Arg.Any<PushType>(), Arg.Any<object>(),
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<ClientType?>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(0)
            .SendPayloadToInstallationAsync(Arg.Any<string>(), Arg.Any<PushType>(), Arg.Any<object>(),
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<ClientType?>());
    }

    [Theory]
    [BitAutoData]
    public async Task SendAsync_UserIdAndOrganizationIdAndInstallationIdEmpty_NoPushNotificationSent(
        SutProvider<PushController> sutProvider, Guid installationId)
    {
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = false;
        sutProvider.GetDependency<ICurrentContext>().InstallationId.Returns(installationId);

        await sutProvider.Sut.SendAsync(new PushSendRequestModel
        {
            Type = PushType.SyncNotification,
            UserId = null,
            OrganizationId = null,
            InstallationId = null,
            Payload = "test-payload"
        });

        await sutProvider.GetDependency<IPushNotificationService>().Received(0)
            .SendPayloadToUserAsync(Arg.Any<string>(), Arg.Any<PushType>(), Arg.Any<object>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<ClientType?>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(0)
            .SendPayloadToOrganizationAsync(Arg.Any<string>(), Arg.Any<PushType>(), Arg.Any<object>(),
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<ClientType?>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(0)
            .SendPayloadToInstallationAsync(Arg.Any<string>(), Arg.Any<PushType>(), Arg.Any<object>(),
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<ClientType?>());
    }

    [Theory]
    [RepeatingPatternBitAutoData([false, true], [false, true], [false, true])]
    public async Task SendAsync_UserIdSet_SendPayloadToUserAsync(bool haveIdentifier, bool haveDeviceId,
        bool haveOrganizationId, SutProvider<PushController> sutProvider, Guid installationId, Guid userId,
        Guid identifier, Guid deviceId)
    {
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = false;
        sutProvider.GetDependency<ICurrentContext>().InstallationId.Returns(installationId);

        var expectedUserId = $"{installationId}_{userId}";
        var expectedIdentifier = haveIdentifier ? $"{installationId}_{identifier}" : null;
        var expectedDeviceId = haveDeviceId ? $"{installationId}_{deviceId}" : null;

        await sutProvider.Sut.SendAsync(new PushSendRequestModel
        {
            Type = PushType.SyncNotification,
            UserId = userId.ToString(),
            OrganizationId = haveOrganizationId ? Guid.NewGuid().ToString() : null,
            InstallationId = null,
            Payload = "test-payload",
            DeviceId = haveDeviceId ? deviceId.ToString() : null,
            Identifier = haveIdentifier ? identifier.ToString() : null,
            ClientType = ClientType.All,
        });

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .SendPayloadToUserAsync(expectedUserId, PushType.SyncNotification, "test-payload", expectedIdentifier,
                expectedDeviceId, ClientType.All);
        await sutProvider.GetDependency<IPushNotificationService>().Received(0)
            .SendPayloadToOrganizationAsync(Arg.Any<string>(), Arg.Any<PushType>(), Arg.Any<object>(),
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<ClientType?>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(0)
            .SendPayloadToInstallationAsync(Arg.Any<string>(), Arg.Any<PushType>(), Arg.Any<object>(),
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<ClientType?>());
    }

    [Theory]
    [RepeatingPatternBitAutoData([false, true], [false, true])]
    public async Task SendAsync_OrganizationIdSet_SendPayloadToOrganizationAsync(bool haveIdentifier, bool haveDeviceId,
        SutProvider<PushController> sutProvider, Guid installationId, Guid organizationId, Guid identifier,
        Guid deviceId)
    {
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = false;
        sutProvider.GetDependency<ICurrentContext>().InstallationId.Returns(installationId);

        var expectedOrganizationId = $"{installationId}_{organizationId}";
        var expectedIdentifier = haveIdentifier ? $"{installationId}_{identifier}" : null;
        var expectedDeviceId = haveDeviceId ? $"{installationId}_{deviceId}" : null;

        await sutProvider.Sut.SendAsync(new PushSendRequestModel
        {
            Type = PushType.SyncNotification,
            UserId = null,
            OrganizationId = organizationId.ToString(),
            InstallationId = null,
            Payload = "test-payload",
            DeviceId = haveDeviceId ? deviceId.ToString() : null,
            Identifier = haveIdentifier ? identifier.ToString() : null,
            ClientType = ClientType.All,
        });

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .SendPayloadToOrganizationAsync(expectedOrganizationId, PushType.SyncNotification, "test-payload",
                expectedIdentifier, expectedDeviceId, ClientType.All);
        await sutProvider.GetDependency<IPushNotificationService>().Received(0)
            .SendPayloadToUserAsync(Arg.Any<string>(), Arg.Any<PushType>(), Arg.Any<object>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<ClientType?>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(0)
            .SendPayloadToInstallationAsync(Arg.Any<string>(), Arg.Any<PushType>(), Arg.Any<object>(),
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<ClientType?>());
    }

    [Theory]
    [RepeatingPatternBitAutoData([false, true], [false, true])]
    public async Task SendAsync_InstallationIdSet_SendPayloadToInstallationAsync(bool haveIdentifier, bool haveDeviceId,
        SutProvider<PushController> sutProvider, Guid installationId, Guid identifier, Guid deviceId)
    {
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = false;
        sutProvider.GetDependency<ICurrentContext>().InstallationId.Returns(installationId);

        var expectedIdentifier = haveIdentifier ? $"{installationId}_{identifier}" : null;
        var expectedDeviceId = haveDeviceId ? $"{installationId}_{deviceId}" : null;

        await sutProvider.Sut.SendAsync(new PushSendRequestModel
        {
            Type = PushType.SyncNotification,
            UserId = null,
            OrganizationId = null,
            InstallationId = installationId.ToString(),
            Payload = "test-payload",
            DeviceId = haveDeviceId ? deviceId.ToString() : null,
            Identifier = haveIdentifier ? identifier.ToString() : null,
            ClientType = ClientType.All,
        });

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .SendPayloadToInstallationAsync(installationId.ToString(), PushType.SyncNotification, "test-payload",
                expectedIdentifier, expectedDeviceId, ClientType.All);
        await sutProvider.GetDependency<IPushNotificationService>().Received(0)
            .SendPayloadToOrganizationAsync(Arg.Any<string>(), Arg.Any<PushType>(), Arg.Any<object>(),
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<ClientType?>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(0)
            .SendPayloadToUserAsync(Arg.Any<string>(), Arg.Any<PushType>(), Arg.Any<object>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<ClientType?>());
    }

    [Theory]
    [BitAutoData]
    public async Task SendAsync_InstallationIdNotMatching_BadRequest(SutProvider<PushController> sutProvider,
        Guid installationId)
    {
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = false;
        sutProvider.GetDependency<ICurrentContext>().InstallationId.Returns(installationId);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.SendAsync(new PushSendRequestModel
            {
                Type = PushType.SyncNotification,
                UserId = null,
                OrganizationId = null,
                InstallationId = Guid.NewGuid().ToString(),
                Payload = "test-payload",
                DeviceId = null,
                Identifier = null,
                ClientType = ClientType.All,
            }));

        Assert.Equal("InstallationId does not match current context.", exception.Message);

        await sutProvider.GetDependency<IPushNotificationService>().Received(0)
            .SendPayloadToInstallationAsync(Arg.Any<string>(), Arg.Any<PushType>(), Arg.Any<object>(),
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<ClientType?>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(0)
            .SendPayloadToOrganizationAsync(Arg.Any<string>(), Arg.Any<PushType>(), Arg.Any<object>(),
                Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<ClientType?>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(0)
            .SendPayloadToUserAsync(Arg.Any<string>(), Arg.Any<PushType>(), Arg.Any<object>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<ClientType?>());
    }

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
                Identifier = identifier.ToString()
            }));

        Assert.Equal("Not correctly configured for push relays.", exception.Message);

        await sutProvider.GetDependency<IPushRegistrationService>().Received(0)
            .CreateOrUpdateRegistrationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<DeviceType>(), Arg.Any<IEnumerable<string>>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async Task? RegisterAsync_ValidModel_CreatedOrUpdatedRegistration(SutProvider<PushController> sutProvider,
        Guid installationId, Guid userId, Guid identifier, Guid deviceId, Guid organizationId)
    {
        sutProvider.GetDependency<IGlobalSettings>().SelfHosted = false;
        sutProvider.GetDependency<ICurrentContext>().InstallationId.Returns(installationId);

        var expectedUserId = $"{installationId}_{userId}";
        var expectedIdentifier = $"{installationId}_{identifier}";
        var expectedDeviceId = $"{installationId}_{deviceId}";
        var expectedOrganizationId = $"{installationId}_{organizationId}";

        await sutProvider.Sut.RegisterAsync(new PushRegistrationRequestModel
        {
            DeviceId = deviceId.ToString(),
            PushToken = "test-push-token",
            UserId = userId.ToString(),
            Type = DeviceType.Android,
            Identifier = identifier.ToString(),
            OrganizationIds = [organizationId.ToString()],
            InstallationId = installationId.ToString()
        });

        await sutProvider.GetDependency<IPushRegistrationService>().Received(1)
            .CreateOrUpdateRegistrationAsync("test-push-token", expectedDeviceId, expectedUserId,
                expectedIdentifier, DeviceType.Android, Arg.Do<IEnumerable<string>>(organizationIds =>
                {
                    var organizationIdsList = organizationIds.ToList();
                    Assert.Contains(expectedOrganizationId, organizationIdsList);
                    Assert.Single(organizationIdsList);
                }), installationId.ToString());
    }
}
