using Bit.Commercial.Core.SecretManagerFeatures.Secrets;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretManagerFeatures.Secrets
{
    [SutProviderCustomize]
    public class UpdateSecretCommandTests
    {
        [Theory]
        [BitAutoData]
        public async Task UpdateAsync_DefaultGuidId_ThrowsNotFound(Secret data,
          SutProvider<UpdateSecretCommand> sutProvider)
        {
            data.Id = new Guid();

            var exception = await Assert.ThrowsAsync<Exception>(() => sutProvider.Sut.UpdateAsync(data));

            Assert.Contains("Cannot update secret, secret does not exist.", exception.Message);
            await sutProvider.GetDependency<ISecretRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        }

        [Theory]
        [BitAutoData]
        public async Task UpdateAsync_SecretDoesNotExist_ThrowsNotFound(Secret data,
          SutProvider<UpdateSecretCommand> sutProvider)
        {
            var exception = await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(data));

            await sutProvider.GetDependency<ISecretRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
        }

        [Theory]
        [BitAutoData]
        public async Task UpdateAsync_CallsReplaceAsync(Secret data,
          SutProvider<UpdateSecretCommand> sutProvider)
        {
            sutProvider.GetDependency<ISecretRepository>().GetByIdAsync(data.Id).Returns(data);
            await sutProvider.Sut.UpdateAsync(data);

            await sutProvider.GetDependency<ISecretRepository>().Received(1)
                .ReplaceAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
        }
    }
}

