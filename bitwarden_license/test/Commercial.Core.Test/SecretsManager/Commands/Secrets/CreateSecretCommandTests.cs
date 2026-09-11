using Bit.Commercial.Core.SecretsManager.Commands.Secrets;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.Secrets;

[SutProviderCustomize]
[SecretCustomize]
public class CreateSecretCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task CreateAsync_WritesInitialVersionInsideCreate(Secret data,
        SutProvider<CreateSecretCommand> sutProvider, Project mockProject, SecretVersion builtVersion, Guid userId)
    {
        data.Projects = new List<Project>() { mockProject };
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<IBuildSecretVersionCommand>()
            .BuildAsync(Arg.Any<Secret>(), Arg.Any<Guid>())
            .Returns(builtVersion);

        await sutProvider.Sut.CreateAsync(data, null);

        await sutProvider.GetDependency<IBuildSecretVersionCommand>().Received(1)
            .BuildAsync(data, userId);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .CreateAsync(data, null, builtVersion);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_WithoutIdentifiedClient_Throws(Secret data,
        SutProvider<CreateSecretCommand> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(data, null));

        await sutProvider.GetDependency<ISecretRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<Secret>(), Arg.Any<SecretAccessPoliciesUpdates>(), Arg.Any<SecretVersion>());
    }
}
