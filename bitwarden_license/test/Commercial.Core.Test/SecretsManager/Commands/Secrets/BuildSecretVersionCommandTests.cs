using Bit.Commercial.Core.SecretsManager.Commands.Secrets;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.Secrets;

[SutProviderCustomize]
[SecretCustomize]
public class BuildSecretVersionCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task BuildAsync_RecordsValueAndRevisionDate(
        SutProvider<BuildSecretVersionCommand> sutProvider, Secret secret, Guid accessClientId,
        OrganizationUser organizationUser)
    {
        secret.Value = "current-value";
        secret.RevisionDate = new DateTime(2026, 6, 15, 12, 30, 0, DateTimeKind.Utc);

        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.User);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(secret.OrganizationId, accessClientId)
            .Returns(organizationUser);

        var version = await sutProvider.Sut.BuildAsync(secret, accessClientId);

        Assert.Equal(secret.Id, version.SecretId);
        Assert.Equal("current-value", version.Value);
        Assert.Equal(secret.RevisionDate, version.VersionDate);
    }

    // This command cannot persist a version: it takes no version repository, so the only path to
    // the database is ISecretRepository writing it inside the secret's own transaction. That is
    // enforced by the constructor rather than by a test.

    [Theory]
    [BitAutoData]
    public async Task BuildAsync_ServiceAccountClient_AttributesToServiceAccount(
        SutProvider<BuildSecretVersionCommand> sutProvider, Secret secret, Guid accessClientId)
    {
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType
            .Returns(IdentityClientType.ServiceAccount);

        var version = await sutProvider.Sut.BuildAsync(secret, accessClientId);

        Assert.Equal(accessClientId, version.EditorServiceAccountId);
        Assert.Null(version.EditorOrganizationUserId);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .GetByOrganizationAsync(default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task BuildAsync_UserClient_AttributesToOrganizationUser(
        SutProvider<BuildSecretVersionCommand> sutProvider, Secret secret, Guid accessClientId,
        OrganizationUser organizationUser)
    {
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.User);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(secret.OrganizationId, accessClientId)
            .Returns(organizationUser);

        var version = await sutProvider.Sut.BuildAsync(secret, accessClientId);

        Assert.Equal(organizationUser.Id, version.EditorOrganizationUserId);
        Assert.Null(version.EditorServiceAccountId);
    }

    [Theory]
    [BitAutoData]
    public async Task BuildAsync_UnattributableClient_StillBuildsVersionWithoutEditor(
        SutProvider<BuildSecretVersionCommand> sutProvider, Secret secret, Guid accessClientId)
    {
        // An organization API key authenticates as the organization, so it matches no
        // OrganizationUser. The version must still be built, just without an editor.
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType
            .Returns(IdentityClientType.Organization);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(secret.OrganizationId, accessClientId)
            .Returns((OrganizationUser)null);

        var version = await sutProvider.Sut.BuildAsync(secret, accessClientId);

        Assert.NotNull(version);
        Assert.Null(version.EditorOrganizationUserId);
        Assert.Null(version.EditorServiceAccountId);
    }
}
