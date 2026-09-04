using Bit.Commercial.Core.SecretsManager.Commands.Secrets;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Test.SecretsManager.AutoFixture.SecretsFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Commands.Secrets;

[SutProviderCustomize]
[SecretCustomize]
public class CreateSecretVersionCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task CreateAsync_RecordsCurrentValueAndRevisionDate(
        SutProvider<CreateSecretVersionCommand> sutProvider, Secret secret, Guid accessClientId,
        OrganizationUser organizationUser)
    {
        secret.Value = "current-value";
        secret.RevisionDate = new DateTime(2026, 6, 15, 12, 30, 0, DateTimeKind.Utc);

        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.User);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(secret.OrganizationId, accessClientId)
            .Returns(organizationUser);

        await sutProvider.Sut.CreateAsync(secret, accessClientId);

        await sutProvider.GetDependency<ISecretVersionRepository>().Received(1).CreateAsync(
            Arg.Is<SecretVersion>(v =>
                v.SecretId == secret.Id &&
                v.Value == "current-value" &&
                v.VersionDate == secret.RevisionDate));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_ServiceAccountClient_AttributesToServiceAccount(
        SutProvider<CreateSecretVersionCommand> sutProvider, Secret secret, Guid accessClientId)
    {
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType
            .Returns(IdentityClientType.ServiceAccount);

        await sutProvider.Sut.CreateAsync(secret, accessClientId);

        await sutProvider.GetDependency<ISecretVersionRepository>().Received(1).CreateAsync(
            Arg.Is<SecretVersion>(v =>
                v.EditorServiceAccountId == accessClientId &&
                v.EditorOrganizationUserId == null));

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .GetByOrganizationAsync(default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_UserClient_AttributesToOrganizationUser(
        SutProvider<CreateSecretVersionCommand> sutProvider, Secret secret, Guid accessClientId,
        OrganizationUser organizationUser)
    {
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType.Returns(IdentityClientType.User);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(secret.OrganizationId, accessClientId)
            .Returns(organizationUser);

        await sutProvider.Sut.CreateAsync(secret, accessClientId);

        await sutProvider.GetDependency<ISecretVersionRepository>().Received(1).CreateAsync(
            Arg.Is<SecretVersion>(v =>
                v.EditorOrganizationUserId == organizationUser.Id &&
                v.EditorServiceAccountId == null));
    }

    [Theory]
    [BitAutoData]
    public async Task CreateAsync_UnattributableClient_StillRecordsVersionWithoutEditor(
        SutProvider<CreateSecretVersionCommand> sutProvider, Secret secret, Guid accessClientId)
    {
        // An organization API key authenticates as the organization, so it matches no
        // OrganizationUser. The version must still be recorded, just without an editor.
        sutProvider.GetDependency<ICurrentContext>().IdentityClientType
            .Returns(IdentityClientType.Organization);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(secret.OrganizationId, accessClientId)
            .Returns((OrganizationUser)null);

        await sutProvider.Sut.CreateAsync(secret, accessClientId);

        await sutProvider.GetDependency<ISecretVersionRepository>().Received(1).CreateAsync(
            Arg.Is<SecretVersion>(v =>
                v.EditorOrganizationUserId == null &&
                v.EditorServiceAccountId == null));
    }
}
