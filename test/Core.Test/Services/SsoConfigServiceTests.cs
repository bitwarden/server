using System;
using System.Threading.Tasks;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using SsoConfig = Bit.Core.Models.Table.SsoConfig;

namespace Bit.Core.Test.Services
{
    public class SsoConfigServiceTests
    {
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_UpsertInRepository(SsoConfig ssoConfig, SutProvider<SsoConfigService> sutProvider)
        {
            var creationDate = ssoConfig.CreationDate;

            await sutProvider.Sut.SaveAsync(ssoConfig);

            await sutProvider.GetDependency<ISsoConfigRepository>().Received().UpsertAsync(ssoConfig);
            Assert.Equal(ssoConfig.CreationDate, creationDate);
            Assert.True(ssoConfig.RevisionDate - DateTime.UtcNow < TimeSpan.FromSeconds(1));
        }


        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_DefaultId_UpsertInRepository(SsoConfig ssoConfig, SutProvider<SsoConfigService> sutProvider)
        {
            ssoConfig.Id = default;

            await sutProvider.Sut.SaveAsync(ssoConfig);

            await sutProvider.GetDependency<ISsoConfigRepository>().Received().UpsertAsync(ssoConfig);
            Assert.True(ssoConfig.CreationDate - DateTime.UtcNow < TimeSpan.FromSeconds(1));
            Assert.True(ssoConfig.RevisionDate - DateTime.UtcNow < TimeSpan.FromSeconds(1));
        }
    }
}
