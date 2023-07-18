using Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.AccessPolicies;

[SutProviderCustomize]
[ProjectCustomize]
public class DeleteAccessPolicyCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteAsync_Success(SutProvider<DeleteAccessPolicyCommand> sutProvider, Guid data)
    {
        await sutProvider.Sut.DeleteAsync(data);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .DeleteAsync(Arg.Is(AssertHelper.AssertPropertyEqual(data)));
    }
}
