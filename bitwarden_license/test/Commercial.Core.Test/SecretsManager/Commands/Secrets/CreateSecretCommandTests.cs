using Bit.Commercial.Core.SecretsManager.Commands.Secrets;
using Bit.Core;
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
        SetVersioningEnabled(sutProvider, true);
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
    public async Task CreateAsync_VersioningDisabled_WritesNoVersion(Secret data,
        SutProvider<CreateSecretCommand> sutProvider, Project mockProject, Guid userId)
    {
        data.Projects = new List<Project>() { mockProject };
        SetVersioningEnabled(sutProvider, false);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);

        await sutProvider.Sut.CreateAsync(data, null);

        await sutProvider.GetDependency<IBuildSecretVersionCommand>().DidNotReceiveWithAnyArgs()
            .BuildAsync(Arg.Any<Secret>(), Arg.Any<Guid>());

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .CreateAsync(data, null, null);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_VersioningDisabledWithoutIdentifiedClient_StillCreates(Secret data,
        SutProvider<CreateSecretCommand> sutProvider, Project mockProject)
    {
        // With versioning off there is no version to attribute, so an unidentified client must not
        // be turned away from a plain create.
        data.Projects = new List<Project>() { mockProject };
        SetVersioningEnabled(sutProvider, false);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        await sutProvider.Sut.CreateAsync(data, null);

        await sutProvider.GetDependency<ISecretRepository>().Received(1)
            .CreateAsync(data, null, null);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_WithoutIdentifiedClient_Throws(Secret data,
        SutProvider<CreateSecretCommand> sutProvider)
    {
        SetVersioningEnabled(sutProvider, true);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(data, null));

        await sutProvider.GetDependency<ISecretRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(Arg.Any<Secret>(), Arg.Any<SecretAccessPoliciesUpdates>(), Arg.Any<SecretVersion>());
    }

    private static void SetVersioningEnabled(SutProvider<CreateSecretCommand> sutProvider, bool enabled)
    {
        sutProvider.GetDependency<Bitwarden.Server.Sdk.Features.IFeatureService>()
            .IsEnabled(FeatureFlagKeys.SecretsVersioning)
            .Returns(enabled);
    }
}
