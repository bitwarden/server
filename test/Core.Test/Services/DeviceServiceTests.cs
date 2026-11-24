using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.PushRegistration;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class DeviceServiceTests
{
    [Theory]
    [BitAutoData]
    public async Task SaveAsync_IdProvided_UpdatedRevisionDateAndPushRegistration(Guid id, Guid userId,
        Guid organizationId1, Guid organizationId2, Guid installationId,
        OrganizationUserOrganizationDetails organizationUserOrganizationDetails1,
        OrganizationUserOrganizationDetails organizationUserOrganizationDetails2)
    {
        organizationUserOrganizationDetails1.OrganizationId = organizationId1;
        organizationUserOrganizationDetails2.OrganizationId = organizationId2;

        var deviceRepo = Substitute.For<IDeviceRepository>();
        var pushRepo = Substitute.For<IPushRegistrationService>();
        var organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        organizationUserRepository.GetManyDetailsByUserAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType?>())
            .Returns([organizationUserOrganizationDetails1, organizationUserOrganizationDetails2]);
        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.Installation.Id.Returns(installationId);
        var deviceService = new DeviceService(deviceRepo, pushRepo, organizationUserRepository, globalSettings);

        var device = new Device
        {
            Id = id,
            Name = "test device",
            Type = DeviceType.Android,
            UserId = userId,
            PushToken = "testToken",
            Identifier = "testid"
        };
        await deviceService.SaveAsync(device);

        Assert.True(device.RevisionDate - DateTime.UtcNow < TimeSpan.FromSeconds(1));
        await pushRepo.Received(1).CreateOrUpdateRegistrationAsync(Arg.Is<PushRegistrationData>(v => v.Token == "testToken"), id.ToString(),
            userId.ToString(), "testid", DeviceType.Android,
            Arg.Do<IEnumerable<string>>(organizationIds =>
            {
                var organizationIdsList = organizationIds.ToList();
                Assert.Equal(2, organizationIdsList.Count);
                Assert.Contains(organizationId1.ToString(), organizationIdsList);
                Assert.Contains(organizationId2.ToString(), organizationIdsList);
            }), installationId);
    }

    [Theory]
    [BitAutoData]
    public async Task SaveAsync_IdNotProvided_CreatedAndPushRegistration(Guid userId, Guid organizationId1,
        Guid organizationId2, Guid installationId,
        OrganizationUserOrganizationDetails organizationUserOrganizationDetails1,
        OrganizationUserOrganizationDetails organizationUserOrganizationDetails2)
    {
        organizationUserOrganizationDetails1.OrganizationId = organizationId1;
        organizationUserOrganizationDetails2.OrganizationId = organizationId2;

        var deviceRepo = Substitute.For<IDeviceRepository>();
        var pushRepo = Substitute.For<IPushRegistrationService>();
        var organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        organizationUserRepository.GetManyDetailsByUserAsync(Arg.Any<Guid>(), Arg.Any<OrganizationUserStatusType?>())
            .Returns([organizationUserOrganizationDetails1, organizationUserOrganizationDetails2]);
        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.Installation.Id.Returns(installationId);
        var deviceService = new DeviceService(deviceRepo, pushRepo, organizationUserRepository, globalSettings);

        var device = new Device
        {
            Name = "test device",
            Type = DeviceType.Android,
            UserId = userId,
            PushToken = "testToken",
            Identifier = "testid"
        };
        await deviceService.SaveAsync(device);

        await pushRepo.Received(1).CreateOrUpdateRegistrationAsync(Arg.Is<PushRegistrationData>(v => v.Token == "testToken"),
            Arg.Do<string>(id => Guid.TryParse(id, out var _)), userId.ToString(), "testid", DeviceType.Android,
            Arg.Do<IEnumerable<string>>(organizationIds =>
            {
                var organizationIdsList = organizationIds.ToList();
                Assert.Equal(2, organizationIdsList.Count);
                Assert.Contains(organizationId1.ToString(), organizationIdsList);
                Assert.Contains(organizationId2.ToString(), organizationIdsList);
            }), installationId);
    }
}
