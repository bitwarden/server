using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Enums.Provider;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business.Provider;
using Bit.Core.Models.Table;
using Bit.Core.Models.Table.Provider;
using Bit.Core.Repositories;
using Bit.Core.Repositories.SqlServer;
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
    }
}
