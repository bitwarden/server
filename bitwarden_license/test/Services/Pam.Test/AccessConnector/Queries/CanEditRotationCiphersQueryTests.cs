using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
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

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task CanEditAllAsync_OwnerOrAdminWithAccessToAllItems_ReturnsTrueWithoutReadingPermissions(
        OrganizationUserType type, Guid organizationId, Guid userId, Guid cipherId,
        SutProvider<CanEditRotationCiphersQuery> sutProvider)
    {
        SetMembership(sutProvider, organizationId, userId,
            new CurrentContextOrganization { Id = organizationId, Type = type });
        SetAbility(sutProvider, organizationId, enabled: true, allowAdminAccessToAllCollectionItems: true);

        Assert.True(await sutProvider.Sut.CanEditAllAsync(organizationId, [cipherId]));

        await sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().DidNotReceiveWithAnyArgs()
            .GetByOrganization(default);
    }

    [Theory, BitAutoData]
    public async Task CanEditAllAsync_CustomUserWithEditAnyCollection_ReturnsTrueWithoutReadingPermissions(
        Guid organizationId, Guid userId, Guid cipherId, SutProvider<CanEditRotationCiphersQuery> sutProvider)
    {
        SetMembership(sutProvider, organizationId, userId, new CurrentContextOrganization
        {
            Id = organizationId,
            Type = OrganizationUserType.Custom,
            Permissions = new Permissions { EditAnyCollection = true },
        });
        SetAbility(sutProvider, organizationId, enabled: true, allowAdminAccessToAllCollectionItems: false);

        Assert.True(await sutProvider.Sut.CanEditAllAsync(organizationId, [cipherId]));

        await sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().DidNotReceiveWithAnyArgs()
            .GetByOrganization(default);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin, true, false)]
    [BitAutoData(OrganizationUserType.User, true, true)]
    [BitAutoData(OrganizationUserType.Custom, true, true)]
    [BitAutoData(OrganizationUserType.Owner, false, true)]
    public async Task CanEditAllAsync_NotGrantedEveryCipher_ReadsPermissions(
        OrganizationUserType type, bool enabled, bool allowAdminAccessToAllCollectionItems,
        Guid organizationId, Guid userId, Guid visibleId, Guid hiddenId,
        SutProvider<CanEditRotationCiphersQuery> sutProvider)
    {
        SetMembership(sutProvider, organizationId, userId,
            new CurrentContextOrganization { Id = organizationId, Type = type });
        SetAbility(sutProvider, organizationId, enabled, allowAdminAccessToAllCollectionItems);
        SetPermissions(sutProvider, organizationId, Permission(visibleId, edit: true, viewPassword: true));

        Assert.False(await sutProvider.Sut.CanEditAllAsync(organizationId, [hiddenId]));

        await sutProvider.GetDependency<IGetCipherPermissionsForUserQuery>().Received(1)
            .GetByOrganization(organizationId);
    }

    private static void SetMembership(
        SutProvider<CanEditRotationCiphersQuery> sutProvider, Guid organizationId, Guid userId,
        CurrentContextOrganization organization)
    {
        var currentContext = sutProvider.GetDependency<ICurrentContext>();
        currentContext.UserId.Returns(userId);
        currentContext.GetOrganization(organizationId).Returns(organization);
    }

    private static void SetAbility(
        SutProvider<CanEditRotationCiphersQuery> sutProvider, Guid organizationId, bool enabled,
        bool allowAdminAccessToAllCollectionItems)
    {
        sutProvider.GetDependency<IOrganizationAbilityCacheService>().GetOrganizationAbilityAsync(organizationId)
            .Returns(new OrganizationAbility
            {
                Id = organizationId,
                Enabled = enabled,
                AllowAdminAccessToAllCollectionItems = allowAdminAccessToAllCollectionItems,
            });
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
