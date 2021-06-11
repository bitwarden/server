using System;
using System.Threading.Tasks;
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
                Data = "TESTDATA",
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
                Data = "TESTDATA",
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
    }
}
