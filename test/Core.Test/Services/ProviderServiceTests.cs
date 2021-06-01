using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Enums.Provider;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business.Provider;
using Bit.Core.Models.Table;
using Bit.Core.Models.Table.Provider;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.ProviderUserFixtures;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;
using Xunit;
using ProviderUser = Bit.Core.Models.Table.Provider.ProviderUser;

namespace Bit.Core.Test.Services
{
    public class ProviderServiceTests
    {
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task CreateAsync_UserIdIsInvalid_Throws(SutProvider<ProviderService> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.CreateAsync(default));
            Assert.Contains("Invalid owner.", exception.Message);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task CreateAsync_Success(User user, SutProvider<ProviderService> sutProvider)
        {
            var userService = sutProvider.GetDependency<IUserService>();
            userService.GetUserByIdAsync(user.Id).Returns(user); 
            
            await sutProvider.Sut.CreateAsync(user.Id);
            
            await sutProvider.GetDependency<IProviderRepository>().ReceivedWithAnyArgs().CreateAsync(default);
            await sutProvider.GetDependency<IMailService>().ReceivedWithAnyArgs().SendProviderSetupInviteEmailAsync(default, default, default);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task CompleteSetupAsync_UserIdIsInvalid_Throws(SutProvider<ProviderService> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.CompleteSetupAsync(default, default, default, default));
            Assert.Contains("Invalid owner.", exception.Message);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task CompleteSetupAsync_TokenIsInvalid_Throws(User user, Provider provider,
            SutProvider<ProviderService> sutProvider)
        {
            var userService = sutProvider.GetDependency<IUserService>();
            userService.GetUserByIdAsync(user.Id).Returns(user); 

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.CompleteSetupAsync(provider, user.Id, default, default));
            Assert.Contains("Invalid token.", exception.Message);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task CompleteSetupAsync_Success(User user, Provider provider,
            SutProvider<ProviderService> sutProvider)
        {
            var userService = sutProvider.GetDependency<IUserService>();
            userService.GetUserByIdAsync(user.Id).Returns(user);

            var dataProtectionProvider = DataProtectionProvider.Create("ApplicationName");
            var protector = dataProtectionProvider.CreateProtector("ProviderServiceDataProtector");
            sutProvider.GetDependency<IDataProtectionProvider>().CreateProtector("ProviderServiceDataProtector")
                .Returns(protector);
            sutProvider.Create();

            var token = protector.Protect($"ProviderSetupInvite {provider.Id} {user.Email} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");
            
            await sutProvider.Sut.CompleteSetupAsync(provider, user.Id, token, default);

            await sutProvider.GetDependency<IProviderRepository>().Received().UpsertAsync(provider);
            await sutProvider.GetDependency<IProviderUserRepository>().Received()
                .CreateAsync(Arg.Is<ProviderUser>(pu => pu.UserId == user.Id && pu.ProviderId == provider.Id));
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task UpdateAsync_ProviderIdIsInvalid_Throws(Provider provider, SutProvider<ProviderService> sutProvider)
        {
            provider.Id = default;
            
            var exception = await Assert.ThrowsAsync<ApplicationException>(
                () => sutProvider.Sut.UpdateAsync(provider));
            Assert.Contains("Cannot create provider this way.", exception.Message);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task UpdateAsync_Success(Provider provider, SutProvider<ProviderService> sutProvider)
        {
            await sutProvider.Sut.UpdateAsync(provider);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task InviteUserAsync_ProviderIdIsInvalid_Throws(Provider provider, SutProvider<ProviderService> sutProvider)
        {
            provider.Id = default;
            
            await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.InviteUserAsync(provider.Id, default, default));
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task InviteUserAsync_EmailsInvalid_Throws(Provider provider, ProviderUserInvite providerUserInvite,
            SutProvider<ProviderService> sutProvider)
        {
            var providerRepository = sutProvider.GetDependency<IProviderRepository>();
            providerRepository.GetByIdAsync(provider.Id).Returns(provider);

            providerUserInvite.Emails = null;
            
            await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.InviteUserAsync(provider.Id, default, providerUserInvite));
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task InviteUserAsync_AlreadyInvited(Provider provider, ProviderUserInvite providerUserInvite,
            SutProvider<ProviderService> sutProvider)
        {
            var providerRepository = sutProvider.GetDependency<IProviderRepository>();
            providerRepository.GetByIdAsync(provider.Id).Returns(provider);
            var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
            providerUserRepository.GetCountByProviderAsync(default, default, default).ReturnsForAnyArgs(1);

            var result = await sutProvider.Sut.InviteUserAsync(provider.Id, default, providerUserInvite);
            Assert.Empty(result);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task InviteUserAsync_Success(Provider provider, ProviderUserInvite providerUserInvite,
            SutProvider<ProviderService> sutProvider)
        {
            var providerRepository = sutProvider.GetDependency<IProviderRepository>();
            providerRepository.GetByIdAsync(provider.Id).Returns(provider);
            var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
            providerUserRepository.GetCountByProviderAsync(default, default, default).ReturnsForAnyArgs(0);

            var result = await sutProvider.Sut.InviteUserAsync(provider.Id, default, providerUserInvite);
            Assert.Equal(providerUserInvite.Emails.Count(), result.Count);
            Assert.True(result.TrueForAll(pu => pu.Status == ProviderUserStatusType.Invited), "Status must be invited");
            Assert.True(result.TrueForAll(pu => pu.ProviderId == provider.Id), "Provider Id must be correct");
        }
        
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task ResendInvitesAsync_Errors(Provider provider,
            [ProviderUser(ProviderUserStatusType.Invited)]ProviderUser pu1,
            [ProviderUser(ProviderUserStatusType.Accepted)]ProviderUser pu2,
            [ProviderUser(ProviderUserStatusType.Confirmed)]ProviderUser pu3,
            [ProviderUser(ProviderUserStatusType.Invited)]ProviderUser pu4,
            SutProvider<ProviderService> sutProvider)
        {
            var providerUsers = new[] {pu1, pu2, pu3, pu4};
            pu1.ProviderId = pu2.ProviderId = pu3.ProviderId = provider.Id;

            var providerRepository = sutProvider.GetDependency<IProviderRepository>();
            providerRepository.GetByIdAsync(provider.Id).Returns(provider);
            var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
            providerUserRepository.GetManyAsync(default).ReturnsForAnyArgs(providerUsers.ToList());

            var result = await sutProvider.Sut.ResendInvitesAsync(provider.Id, default, providerUsers.Select(pu => pu.Id));
            Assert.Equal("", result[0].Item2);
            Assert.Equal("User invalid.", result[1].Item2);
            Assert.Equal("User invalid.", result[2].Item2);
            Assert.Equal("User invalid.", result[3].Item2);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task ResendInvitesAsync_Success(Provider provider, IEnumerable<ProviderUser> providerUsers,
            SutProvider<ProviderService> sutProvider)
        {
            foreach (var providerUser in providerUsers)
            {
                providerUser.ProviderId = provider.Id;
                providerUser.Status = ProviderUserStatusType.Invited;
            }
            
            var providerRepository = sutProvider.GetDependency<IProviderRepository>();
            providerRepository.GetByIdAsync(provider.Id).Returns(provider);
            var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
            providerUserRepository.GetManyAsync(default).ReturnsForAnyArgs(providerUsers.ToList());

            var result = await sutProvider.Sut.ResendInvitesAsync(provider.Id, default, providerUsers.Select(pu => pu.Id));
            Assert.True(result.All(r => r.Item2 == ""));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task AcceptUserAsync_UserIsInvalid_Throws(ProviderUser providerUser, User user,
            SutProvider<ProviderService> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.AcceptUserAsync(default, default, default));
            Assert.Equal("User invalid.", exception.Message);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task AcceptUserAsync_AlreadyAccepted_Throws(
            [ProviderUser(ProviderUserStatusType.Accepted)]ProviderUser providerUser, User user,
            SutProvider<ProviderService> sutProvider)
        {
            var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
            providerUserRepository.GetByIdAsync(providerUser.Id).Returns(providerUser);
            
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.AcceptUserAsync(providerUser.Id, user, default));
            Assert.Equal("Already accepted.", exception.Message);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task AcceptUserAsync_TokenIsInvalid_Throws(
            [ProviderUser(ProviderUserStatusType.Invited)]ProviderUser providerUser, User user,
            SutProvider<ProviderService> sutProvider)
        {
            var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
            providerUserRepository.GetByIdAsync(providerUser.Id).Returns(providerUser);
            
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.AcceptUserAsync(providerUser.Id, user, default));
            Assert.Equal("Invalid token.", exception.Message);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task AcceptUserAsync_WrongEmail_Throws(
            [ProviderUser(ProviderUserStatusType.Invited)]ProviderUser providerUser, User user,
            SutProvider<ProviderService> sutProvider)
        {
            var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
            providerUserRepository.GetByIdAsync(providerUser.Id).Returns(providerUser);
            
            var dataProtectionProvider = DataProtectionProvider.Create("ApplicationName");
            var protector = dataProtectionProvider.CreateProtector("ProviderServiceDataProtector");
            sutProvider.GetDependency<IDataProtectionProvider>().CreateProtector("ProviderServiceDataProtector")
                .Returns(protector);
            sutProvider.Create();
            
            var token = protector.Protect($"ProviderUserInvite {providerUser.Id} {user.Email} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.AcceptUserAsync(providerUser.Id, user, token));
            Assert.Equal("User email does not match invite.", exception.Message);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task AcceptUserAsync_Success(
            [ProviderUser(ProviderUserStatusType.Invited)]ProviderUser providerUser, User user,
            SutProvider<ProviderService> sutProvider)
        {
            var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
            providerUserRepository.GetByIdAsync(providerUser.Id).Returns(providerUser);
            
            var dataProtectionProvider = DataProtectionProvider.Create("ApplicationName");
            var protector = dataProtectionProvider.CreateProtector("ProviderServiceDataProtector");
            sutProvider.GetDependency<IDataProtectionProvider>().CreateProtector("ProviderServiceDataProtector")
                .Returns(protector);
            sutProvider.Create();

            providerUser.Email = user.Email;
            var token = protector.Protect($"ProviderUserInvite {providerUser.Id} {user.Email} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");

            var pu = await sutProvider.Sut.AcceptUserAsync(providerUser.Id, user, token);
            Assert.Null(pu.Email);
            Assert.Equal(ProviderUserStatusType.Accepted, pu.Status);
            Assert.Equal(user.Id, pu.UserId);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task ConfirmUsersAsync_NoValid(
            [ProviderUser(ProviderUserStatusType.Invited)]ProviderUser pu1,
            [ProviderUser(ProviderUserStatusType.Accepted)]ProviderUser pu2,
            [ProviderUser(ProviderUserStatusType.Confirmed)]ProviderUser pu3,
            SutProvider<ProviderService> sutProvider)
        {
            pu1.ProviderId = pu3.ProviderId;
            var providerUsers = new[] {pu1, pu2, pu3};
            var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
            providerUserRepository.GetManyAsync(default).ReturnsForAnyArgs(providerUsers);

            var dict = providerUsers.ToDictionary(pu => pu.Id, _ => "key");
            var result = await sutProvider.Sut.ConfirmUsersAsync(pu1.ProviderId, dict, default);
            
            Assert.Empty(result);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task ConfirmUsersAsync_Success(
            [ProviderUser(ProviderUserStatusType.Invited)]ProviderUser pu1, User u1,
            [ProviderUser(ProviderUserStatusType.Accepted)]ProviderUser pu2, User u2,
            [ProviderUser(ProviderUserStatusType.Confirmed)]ProviderUser pu3, User u3,
            Provider provider, User user, SutProvider<ProviderService> sutProvider)
        {
            pu1.ProviderId = pu2.ProviderId = pu3.ProviderId = provider.Id;
            pu1.UserId = u1.Id;
            pu2.UserId = u2.Id;
            pu3.UserId = u3.Id;
            var providerUsers = new[] {pu1, pu2, pu3};

            var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
            providerUserRepository.GetManyAsync(default).ReturnsForAnyArgs(providerUsers);
            var providerRepository = sutProvider.GetDependency<IProviderRepository>();
            providerRepository.GetByIdAsync(provider.Id).Returns(provider);
            var userRepository = sutProvider.GetDependency<IUserRepository>();
            userRepository.GetManyAsync(default).ReturnsForAnyArgs(new[] {u1, u2, u3});

            var dict = providerUsers.ToDictionary(pu => pu.Id, _ => "key");
            var result = await sutProvider.Sut.ConfirmUsersAsync(pu1.ProviderId, dict, user.Id);
            
            Assert.Equal("Invalid user.", result[0].Item2);
            Assert.Equal("", result[1].Item2);
            Assert.Equal("Invalid user.", result[2].Item2);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveUserAsync_UserIdIsInvalid_Throws(ProviderUser providerUser,
            SutProvider<ProviderService> sutProvider)
        {
            providerUser.Id = default;
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveUserAsync(providerUser, default));
            Assert.Equal("Invite the user first.", exception.Message);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveUserAsync_Success(
            [ProviderUser(type: ProviderUserType.ProviderAdmin)]ProviderUser providerUser, User savingUser,
            SutProvider<ProviderService> sutProvider)
        {
            var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
            providerUserRepository.GetByIdAsync(providerUser.Id).Returns(providerUser);

            await sutProvider.Sut.SaveUserAsync(providerUser, savingUser.Id);
            await providerUserRepository.Received().ReplaceAsync(providerUser);
            await sutProvider.GetDependency<IEventService>().Received()
                .LogProviderUserEventAsync(providerUser, EventType.ProviderUser_Updated, null);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task DeleteUsersAsync_NoRemainingOwner_Throws(Provider provider, User deletingUser,
            ICollection<ProviderUser> providerUsers, SutProvider<ProviderService> sutProvider)
        {
            var userIds = providerUsers.Select(pu => pu.Id);

            providerUsers.First().UserId = deletingUser.Id;
            foreach (var providerUser in providerUsers)
            {
                providerUser.ProviderId = provider.Id;
            }
            providerUsers.Last().ProviderId = default;
            
            var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
            providerUserRepository.GetManyAsync(default).ReturnsForAnyArgs(providerUsers);
            providerUserRepository.GetManyByProviderAsync(default, default).ReturnsForAnyArgs(new ProviderUser[] {});
            
            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.DeleteUsersAsync(provider.Id, userIds, deletingUser.Id));
            Assert.Equal("Provider must have at least one confirmed ProviderAdmin.", exception.Message);
        }
        
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task DeleteUsersAsync_Success(Provider provider, User deletingUser,            ICollection<ProviderUser> providerUsers,
            [ProviderUser(ProviderUserStatusType.Confirmed, ProviderUserType.ProviderAdmin)]ProviderUser remainingOwner,
            SutProvider<ProviderService> sutProvider)
        {
            var userIds = providerUsers.Select(pu => pu.Id);

            providerUsers.First().UserId = deletingUser.Id;
            foreach (var providerUser in providerUsers)
            {
                providerUser.ProviderId = provider.Id;
            }
            providerUsers.Last().ProviderId = default;
            
            var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
            providerUserRepository.GetManyAsync(default).ReturnsForAnyArgs(providerUsers);
            providerUserRepository.GetManyByProviderAsync(default, default).ReturnsForAnyArgs(new[] {remainingOwner});
            
            var result = await sutProvider.Sut.DeleteUsersAsync(provider.Id, userIds, deletingUser.Id);
            
            Assert.NotEmpty(result);
            Assert.Equal("You cannot remove yourself.", result[0].Item2);
            Assert.Equal("", result[1].Item2);
            Assert.Equal("Invalid user.", result[2].Item2);
        }
    }
}
