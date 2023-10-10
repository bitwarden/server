using Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;
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
public class UpdateAccessPolicyCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_DoesNotExist_ThrowsNotFound(Guid data, bool read, bool write,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(data, read, write));
        await sutProvider.GetDependency<IAccessPolicyRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(Arg.Any<BaseAccessPolicy>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_Success(Guid data, bool read, bool write, UserProjectAccessPolicy accessPolicy,
        SutProvider<UpdateAccessPolicyCommand> sutProvider)
    {
        accessPolicy.Id = data;
        sutProvider.GetDependency<IAccessPolicyRepository>().GetByIdAsync(data).Returns(accessPolicy);

        var result = await sutProvider.Sut.UpdateAsync(data, read, write);

        await sutProvider.GetDependency<IAccessPolicyRepository>().Received(1)
            .ReplaceAsync(Arg.Any<BaseAccessPolicy>());

        AssertHelper.AssertRecent(result.RevisionDate);
        Assert.Equal(read, result.Read);
        Assert.Equal(write, result.Write);
    }
}
