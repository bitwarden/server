using System;
using System.Threading.Tasks;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class SsoConfigServiceTests
    {
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_ExistingItem_UpdatesRevisionDateOnly(SutProvider<SsoConfigService> sutProvider)
        {

            var utcNow = DateTime.UtcNow;

            var ssoConfig = new SsoConfig
            {
                Id = 1,
                Data = "{}",
                Enabled = true,
                OrganizationId = Guid.NewGuid(),
                CreationDate = utcNow.AddDays(-10),
                RevisionDate = utcNow.AddDays(-10),
            };

            sutProvider.GetDependency<ISsoConfigRepository>()
                .UpsertAsync(ssoConfig).Returns(Task.CompletedTask);

            await sutProvider.Sut.SaveAsync(ssoConfig);

            await sutProvider.GetDependency<ISsoConfigRepository>().Received()
                .UpsertAsync(ssoConfig);

            Assert.Equal(utcNow.AddDays(-10), ssoConfig.CreationDate);
            Assert.True(ssoConfig.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_NewItem_UpdatesCreationAndRevisionDate(SutProvider<SsoConfigService> sutProvider)
        {
            var utcNow = DateTime.UtcNow;

            var ssoConfig = new SsoConfig
            {
                Id = default,
                Data = "{}",
                Enabled = true,
                OrganizationId = Guid.NewGuid(),
                CreationDate = utcNow.AddDays(-10),
                RevisionDate = utcNow.AddDays(-10),
            };

            sutProvider.GetDependency<ISsoConfigRepository>()
                .UpsertAsync(ssoConfig).Returns(Task.CompletedTask);

            await sutProvider.Sut.SaveAsync(ssoConfig);

            await sutProvider.GetDependency<ISsoConfigRepository>().Received()
                .UpsertAsync(ssoConfig);

            Assert.True(ssoConfig.CreationDate - utcNow < TimeSpan.FromSeconds(1));
            Assert.True(ssoConfig.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_PreventDisablingKeyConnector(SutProvider<SsoConfigService> sutProvider, Guid orgId)
        {
            var utcNow = DateTime.UtcNow;

            var oldSsoConfig = new SsoConfig
            {
                Id = 1,
                Data = "{\"useKeyConnector\": true}",
                Enabled = true,
                OrganizationId = orgId,
                CreationDate = utcNow.AddDays(-10),
                RevisionDate = utcNow.AddDays(-10),
            };

            var newSsoConfig = new SsoConfig
            {
                Id = 1,
                Data = "{}",
                Enabled = true,
                OrganizationId = orgId,
                CreationDate = utcNow.AddDays(-10),
                RevisionDate = utcNow,
            };

            var ssoConfigRepository = sutProvider.GetDependency<ISsoConfigRepository>();
            ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(oldSsoConfig);
            ssoConfigRepository.UpsertAsync(newSsoConfig).Returns(Task.CompletedTask);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(orgId)
                .Returns(new[] { new OrganizationUserUserDetails { UsesKeyConnector = true } });

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveAsync(newSsoConfig));

            Assert.Contains("Key Connector cannot be disabled at this moment.", exception.Message);

            await sutProvider.GetDependency<ISsoConfigRepository>().DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_AllowDisablingKeyConnectorWhenNoUserIsUsingIt(
            SutProvider<SsoConfigService> sutProvider, Guid orgId)
        {
            var utcNow = DateTime.UtcNow;

            var oldSsoConfig = new SsoConfig
            {
                Id = 1,
                Data = "{\"useKeyConnector\": true}",
                Enabled = true,
                OrganizationId = orgId,
                CreationDate = utcNow.AddDays(-10),
                RevisionDate = utcNow.AddDays(-10),
            };

            var newSsoConfig = new SsoConfig
            {
                Id = 1,
                Data = "{}",
                Enabled = true,
                OrganizationId = orgId,
                CreationDate = utcNow.AddDays(-10),
                RevisionDate = utcNow,
            };

            var ssoConfigRepository = sutProvider.GetDependency<ISsoConfigRepository>();
            ssoConfigRepository.GetByOrganizationIdAsync(orgId).Returns(oldSsoConfig);
            ssoConfigRepository.UpsertAsync(newSsoConfig).Returns(Task.CompletedTask);
            sutProvider.GetDependency<IOrganizationUserRepository>().GetManyDetailsByOrganizationAsync(orgId)
                .Returns(new[] { new OrganizationUserUserDetails { UsesKeyConnector = false } });

            await sutProvider.Sut.SaveAsync(newSsoConfig);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_KeyConnector_SingleOrgNotEnabled_Throws(SutProvider<SsoConfigService> sutProvider)
        {
            var utcNow = DateTime.UtcNow;

            var ssoConfig = new SsoConfig
            {
                Id = default,
                Data = "{\"useKeyConnector\": true}",
                Enabled = true,
                OrganizationId = Guid.NewGuid(),
                CreationDate = utcNow.AddDays(-10),
                RevisionDate = utcNow.AddDays(-10),
            };

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveAsync(ssoConfig));

            Assert.Contains("Key Connector requires the Single Organization policy to be enabled.", exception.Message);

            await sutProvider.GetDependency<ISsoConfigRepository>().DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_KeyConnector_SsoPolicyNotEnabled_Throws(SutProvider<SsoConfigService> sutProvider)
        {
            var utcNow = DateTime.UtcNow;

            var ssoConfig = new SsoConfig
            {
                Id = default,
                Data = "{\"useKeyConnector\": true}",
                Enabled = true,
                OrganizationId = Guid.NewGuid(),
                CreationDate = utcNow.AddDays(-10),
                RevisionDate = utcNow.AddDays(-10),
            };

            sutProvider.GetDependency<IPolicyRepository>().GetByOrganizationIdTypeAsync(
                Arg.Any<Guid>(), Enums.PolicyType.SingleOrg).Returns(new Policy
                {
                    Enabled = true
                });

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveAsync(ssoConfig));

            Assert.Contains("Key Connector requires the Single Sign-On Authentication policy to be enabled.", exception.Message);

            await sutProvider.GetDependency<ISsoConfigRepository>().DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_KeyConnector_SsoConfigNotEnabled_Throws(SutProvider<SsoConfigService> sutProvider)
        {
            var utcNow = DateTime.UtcNow;

            var ssoConfig = new SsoConfig
            {
                Id = default,
                Data = "{\"useKeyConnector\": true}",
                Enabled = false,
                OrganizationId = Guid.NewGuid(),
                CreationDate = utcNow.AddDays(-10),
                RevisionDate = utcNow.AddDays(-10),
            };

            sutProvider.GetDependency<IPolicyRepository>().GetByOrganizationIdTypeAsync(
                Arg.Any<Guid>(), Arg.Any<Enums.PolicyType>()).Returns(new Policy
                {
                    Enabled = true
                });

            var exception = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveAsync(ssoConfig));

            Assert.Contains("You must enable SSO to use Key Connector.", exception.Message);

            await sutProvider.GetDependency<ISsoConfigRepository>().DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
        }
    }
}
