using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Queries;
using Bit.Services.Pam.AccessConnector.Queries;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.AccessConnector.Queries;

[SutProviderCustomize]
public class CanEditRotationCiphersQueryTests
{
    [Theory, BitAutoData]
    public async Task CanEditAllAsync_NoCiphers_ReturnsTrueWithoutReadingPermissions(
        Guid organizationId, SutProvider<CanEditRotationCiphersQuery> sutProvider)
    {
        Assert.True(await sutProvider.Sut.CanEditAllAsync(organizationId, []));

        await sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().DidNotReceiveWithAnyArgs()
            .GetByOrganization(default);
    }

    [Theory, BitAutoData]
    public async Task CanEditAllAsync_EveryCipherEditableWithPassword_ReturnsTrue(
        Guid organizationId, Guid firstId, Guid secondId, SutProvider<CanEditRotationCiphersQuery> sutProvider)
    {
        SetPermissions(sutProvider, organizationId,
            Permission(firstId, edit: true, viewPassword: true),
            Permission(secondId, edit: true, viewPassword: true));

        Assert.True(await sutProvider.Sut.CanEditAllAsync(organizationId, [firstId, secondId]));
    }

    [Theory]
    [BitAutoData(false, true)]
    [BitAutoData(true, false)]
    public async Task CanEditAllAsync_OneCipherNotEditableOrPasswordHidden_ReturnsFalse(
        bool edit, bool viewPassword,
        Guid organizationId, Guid firstId, Guid secondId, SutProvider<CanEditRotationCiphersQuery> sutProvider)
    {
        SetPermissions(sutProvider, organizationId,
            Permission(firstId, edit: true, viewPassword: true),
            Permission(secondId, edit, viewPassword));

        Assert.False(await sutProvider.Sut.CanEditAllAsync(organizationId, [firstId, secondId]));
    }

    [Theory, BitAutoData]
    public async Task CanEditAllAsync_CipherTheUserCannotSee_ReturnsFalse(
        Guid organizationId, Guid visibleId, Guid hiddenId, SutProvider<CanEditRotationCiphersQuery> sutProvider)
    {
        SetPermissions(sutProvider, organizationId, Permission(visibleId, edit: true, viewPassword: true));

        Assert.False(await sutProvider.Sut.CanEditAllAsync(organizationId, [hiddenId]));
    }

    private static OrganizationCipherPermission Permission(Guid id, bool edit, bool viewPassword) =>
        new() { Id = id, Read = true, Edit = edit, ViewPassword = viewPassword };

    private static void SetPermissions(
        SutProvider<CanEditRotationCiphersQuery> sutProvider, Guid organizationId,
        params OrganizationCipherPermission[] permissions)
    {
        sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().GetByOrganization(organizationId)
            .Returns(permissions.ToDictionary(p => p.Id));
    }
}
