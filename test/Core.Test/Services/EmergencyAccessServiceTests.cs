using System;
using System.Threading.Tasks;
using Bit.Core.Exceptions;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class EmergencyAccessServiceTests
    {
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task InviteAsync_UserWithKeyConnectorCannotUseTakeover(
            SutProvider<EmergencyAccessService> sutProvider, User invitingUser, string email, int waitTime)
        {
            invitingUser.UsesKeyConnector = true;
            sutProvider.GetDependency<IUserService>().CanAccessPremium(invitingUser).Returns(true);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.InviteAsync(invitingUser, email, Enums.EmergencyAccessType.Takeover, waitTime));

            Assert.Contains("You cannot use Emergency Access Takeover because you are using Key Connector", exception.Message);
            await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task ConfirmUserAsync_UserWithKeyConnectorCannotUseTakeover(
            SutProvider<EmergencyAccessService> sutProvider, User confirmingUser, string key)
        {
            confirmingUser.UsesKeyConnector = true;
            var emergencyAccess = new EmergencyAccess
            {
                Status = Enums.EmergencyAccessStatusType.Accepted,
                GrantorId = confirmingUser.Id,
                Type = Enums.EmergencyAccessType.Takeover,
            };

            sutProvider.GetDependency<IUserRepository>().GetByIdAsync(confirmingUser.Id).Returns(confirmingUser);
            sutProvider.GetDependency<IEmergencyAccessRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(emergencyAccess);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.ConfirmUserAsync(new Guid(), key, confirmingUser.Id));

            Assert.Contains("You cannot use Emergency Access Takeover because you are using Key Connector", exception.Message);
            await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_UserWithKeyConnectorCannotUseTakeover(
            SutProvider<EmergencyAccessService> sutProvider, User savingUser)
        {
            savingUser.UsesKeyConnector = true;
            var emergencyAccess = new EmergencyAccess
            {
                Type = Enums.EmergencyAccessType.Takeover,
                GrantorId = savingUser.Id,
            };

            sutProvider.GetDependency<IUserService>().GetUserByIdAsync(savingUser.Id).Returns(savingUser);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveAsync(emergencyAccess, savingUser.Id));

            Assert.Contains("You cannot use Emergency Access Takeover because you are using Key Connector", exception.Message);
            await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task InitiateAsync_UserWithKeyConnectorCannotUseTakeover(
            SutProvider<EmergencyAccessService> sutProvider, User initiatingUser, User grantor)
        {
            grantor.UsesKeyConnector = true;
            var emergencyAccess = new EmergencyAccess
            {
                Status = Enums.EmergencyAccessStatusType.Confirmed,
                GranteeId = initiatingUser.Id,
                GrantorId = grantor.Id,
                Type = Enums.EmergencyAccessType.Takeover,
            };

            sutProvider.GetDependency<IEmergencyAccessRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(emergencyAccess);
            sutProvider.GetDependency<IUserRepository>().GetByIdAsync(grantor.Id).Returns(grantor);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.InitiateAsync(new Guid(), initiatingUser));

            Assert.Contains("You cannot takeover an account that is using Key Connector", exception.Message);
            await sutProvider.GetDependency<IEmergencyAccessRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task TakeoverAsync_UserWithKeyConnectorCannotUseTakeover(
            SutProvider<EmergencyAccessService> sutProvider, User requestingUser, User grantor)
        {
            grantor.UsesKeyConnector = true;
            var emergencyAccess = new EmergencyAccess
            {
                GrantorId = grantor.Id,
                GranteeId = requestingUser.Id,
                Status = Enums.EmergencyAccessStatusType.RecoveryApproved,
                Type = Enums.EmergencyAccessType.Takeover,
            };

            sutProvider.GetDependency<IEmergencyAccessRepository>().GetByIdAsync(Arg.Any<Guid>()).Returns(emergencyAccess);
            sutProvider.GetDependency<IUserRepository>().GetByIdAsync(grantor.Id).Returns(grantor);

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.TakeoverAsync(new Guid(), requestingUser));

            Assert.Contains("You cannot takeover an account that is using Key Connector", exception.Message);
        }
    }
}
