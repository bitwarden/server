using Bit.Commercial.Core.SecretManager.Commands.AccessPolicies;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretManager.AccessPolicies;

[SutProviderCustomize]
public class UpdateAccessPolicyCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_Throws_NotFoundException(Guid data, bool read, bool write,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        var exception =
            await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(data, read, write));
        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_Calls_Replace(Guid data, bool read, bool write,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        var existingPolicy = new UserProjectAccessPolicy { Id = data, Read = true, Write = true };
        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data).Returns(existingPolicy);
        var result = await sutProvider.Sut.UpdateAsync(data, read, write);
        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1).ReplaceAsync(existingPolicy);

        AssertHelper.AssertRecent(result.RevisionDate);
        Assert.Equal(read, result.Read);
        Assert.Equal(write, result.Write);
    }
}
