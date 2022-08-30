using Bit.Commercial.Core.SecretManagerFeatures.Secrets;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretManagerFeatures.Secrets
{
    [SutProviderCustomize]
    public class CreateSecretCommandTests
    {
        [Theory]
        [BitAutoData]
        public async Task CreateAsync_CallsCreate(Secret data,
          SutProvider<CreateSecretCommand> sutProvider)
        {
            await sutProvider.Sut.CreateAsync(data);

            await sutProvider.GetDependency<ISecretRepository>().Received(1)
                .CreateAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
        }
    }
}

