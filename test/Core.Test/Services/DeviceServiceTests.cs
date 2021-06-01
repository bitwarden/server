using System;
using System.Threading.Tasks;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture;
using NSubstitute;
using Xunit;
using Device = Bit.Core.Models.Table.Device;

namespace Bit.Core.Test.Services
{
    public class DeviceServiceTests
    {
        [Theory, DeviceAutoData]
        public async Task SaveAsync_DefaultId_CreateInRepository(Device device, SutProvider<DeviceService> sutProvider)
        {
            device.Id = default(Guid);
            var utcNow = DateTime.UtcNow;

            await sutProvider.Sut.SaveAsync(device);

            await sutProvider.GetDependency<IDeviceRepository>().Received().CreateAsync(device);
            await sutProvider.GetDependency<IDeviceRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
            await sutProvider
                .GetDependency<IPushRegistrationService>().Received()
                .CreateOrUpdateRegistrationAsync(device.PushToken, device.Id.ToString(),
                    device.UserId.ToString(), device.Identifier, device.Type);
            Assert.True(device.CreationDate - utcNow < TimeSpan.FromSeconds(1));
            Assert.True(device.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        }

        [Theory, DeviceAutoData]
        public async Task SaveAsync_NonDefaultId_ReplaceInRepository(Device device, SutProvider<DeviceService> sutProvider)
        {
            var creationDate = device.CreationDate;
            var utcNow = DateTime.UtcNow;

            await sutProvider.Sut.SaveAsync(device);

            await sutProvider.GetDependency<IDeviceRepository>().Received().ReplaceAsync(device);
            await sutProvider.GetDependency<IDeviceRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
            await sutProvider
                .GetDependency<IPushRegistrationService>().Received()
                .CreateOrUpdateRegistrationAsync(device.PushToken, device.Id.ToString(),
                    device.UserId.ToString(), device.Identifier, device.Type);
            Assert.Equal(device.CreationDate, creationDate);
            Assert.True(device.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        }

        [Theory, DeviceAutoData]
        public async Task ClearTokenAsync_ClearDeviceAndDeleteRegistration(Device device, SutProvider<DeviceService> sutProvider)
        {
            await sutProvider.Sut.ClearTokenAsync(device);

            await sutProvider.GetDependency<IDeviceRepository>().Received().ClearPushTokenAsync(device.Id);
            await sutProvider.GetDependency<IPushRegistrationService>().Received()
                .DeleteRegistrationAsync(device.Id.ToString());
        }

        [Theory, DeviceAutoData]
        public async Task DeleteAsync_DeleteDeviceAndRegistrationInRepository(Device device, SutProvider<DeviceService> sutProvider)
        {
            await sutProvider.Sut.DeleteAsync(device);

            await sutProvider.GetDependency<IDeviceRepository>().Received().DeleteAsync(device);
            await sutProvider.GetDependency<IPushRegistrationService>().Received()
                .DeleteRegistrationAsync(device.Id.ToString());
        }
    }
}
