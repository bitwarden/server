#nullable enable
using Bit.Commercial.Core.SecretsManager.Commands.Secrets;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.ProjectsFixture;
using Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.Secrets;

[SutProviderCustomize]
[SecretCustomize]
[ProjectCustomize]
public class UpdateSecretCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_ValueUnchanged_RecordsNoVersion(SutProvider<UpdateSecretCommand> sutProvider,
        Secret data, Project project, Guid userId)
    {
        data.Projects = new List<Project> { project };
        SetVersioningEnabled(sutProvider, true);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        await sutProvider.Sut.UpdateAsync(data, null, false);

        await sutProvider.GetDependency<IBuildSecretVersionCommand>().DidNotReceiveWithAnyArgs()
            .BuildAsync(Arg.Any<Secret>(), Arg.Any<Guid>());
        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .UpdateAsync(data, null, null);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_ValueChanged_RecordsVersionInsideUpdate(
        SutProvider<UpdateSecretCommand> sutProvider, Secret data, Project project, SecretVersion builtVersion,
        Guid userId)
    {
        data.Projects = new List<Project> { project };
        SetVersioningEnabled(sutProvider, true);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<IBuildSecretVersionCommand>()
            .BuildAsync(Arg.Any<Secret>(), Arg.Any<Guid>())
            .Returns(builtVersion);

        await sutProvider.Sut.UpdateAsync(data, null, true);

        await sutProvider.GetDependency<IBuildSecretVersionCommand>().Received(1)
            .BuildAsync(data, userId);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .UpdateAsync(data, null, builtVersion);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_ValueChangedWithVersioningDisabled_RecordsNoVersion(
        SutProvider<UpdateSecretCommand> sutProvider, Secret data, Project project, Guid userId)
    {
        data.Projects = new List<Project> { project };
        SetVersioningEnabled(sutProvider, false);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        await sutProvider.Sut.UpdateAsync(data, null, true);

        await sutProvider.GetDependency<IBuildSecretVersionCommand>().DidNotReceiveWithAnyArgs()
            .BuildAsync(Arg.Any<Secret>(), Arg.Any<Guid>());
        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .UpdateAsync(data, null, null);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_ValueChangedWithVersioningDisabledAndUnidentifiedClient_StillUpdates(
        SutProvider<UpdateSecretCommand> sutProvider, Secret data, Project project)
    {
        // With versioning off there is no version to attribute, so an unidentified client must not
        // be turned away from a plain update.
        data.Projects = new List<Project> { project };
        SetVersioningEnabled(sutProvider, false);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        await sutProvider.Sut.UpdateAsync(data, null, true);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .UpdateAsync(data, null, null);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateAsync_ValueChangedWithoutIdentifiedClient_Throws(
        SutProvider<UpdateSecretCommand> sutProvider, Secret data)
    {
        SetVersioningEnabled(sutProvider, true);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateAsync(data, null, true));

        await sutProvider.GetDependency<ISecretRepository>().DidNotReceiveWithAnyArgs()
            .UpdateAsync(Arg.Any<Secret>(), Arg.Any<SecretAccessPoliciesUpdates>(),
                Arg.Any<SecretVersion>());
    }

    private static void SetVersioningEnabled(SutProvider<UpdateSecretCommand> sutProvider, bool enabled)
    {
        sutProvider.GetDependency<Bitwarden.Server.Sdk.Features.IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SecretsVersioning)
            .Returns(enabled);
    }
}
