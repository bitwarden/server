using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Services;

[SutProviderCustomize]
public class CipherPermissionsServiceTests
{
    [Theory, BitAutoData]
    public async Task GetCipherPermissionsAsync_WhenUserOwner_ReturnsFullPermissions(
        SutProvider<CipherPermissionsService> sutProvider,
        User user,
        Cipher cipher)
    {
        cipher.UserId = user.Id;

        var permissions = await sutProvider.Sut.GetCipherPermissionsAsync(cipher, user);

        Assert.True(permissions.Delete);
        Assert.True(permissions.Restore);
    }

    [Theory, BitAutoData]
    public async Task GetCipherPermissionsAsync_WhenNotUserOwnerAndNotOrg_Throws(
        SutProvider<CipherPermissionsService> sutProvider,
        User user,
        Cipher cipher)
    {
        cipher.UserId = Guid.NewGuid(); // Different user
        cipher.OrganizationId = null;

        await Assert.ThrowsAsync<Exception>(() =>
            sutProvider.Sut.GetCipherPermissionsAsync(cipher, user));
    }

    [Theory, BitAutoData]
    public async Task GetCipherPermissionsAsync_WhenOrgCipherNotDetails_Throws(
        SutProvider<CipherPermissionsService> sutProvider,
        User user,
        Cipher cipher)
    {
        cipher.UserId = Guid.NewGuid(); // Different user
        cipher.OrganizationId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sutProvider.Sut.GetCipherPermissionsAsync(cipher, user));
    }

    [Theory, BitAutoData]
    public async Task GetCipherPermissionsAsync_WhenOrgAbilityNotFound_Throws(
        SutProvider<CipherPermissionsService> sutProvider,
        User user,
        CipherDetails cipher)
    {
        cipher.UserId = Guid.NewGuid(); // Different user
        cipher.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(cipher.OrganizationId.Value)
            .Returns((OrganizationAbility)null);

        await Assert.ThrowsAsync<Exception>(() =>
            sutProvider.Sut.GetCipherPermissionsAsync(cipher, user));
    }

    [Theory, BitAutoData]
    public async Task GetCipherPermissionsAsync_WhenLimitedAndManage_ReturnsManagePermissions(
        SutProvider<CipherPermissionsService> sutProvider,
        User user,
        CipherDetails cipher,
        OrganizationAbility orgAbility)
    {
        cipher.UserId = Guid.NewGuid(); // Different user
        cipher.OrganizationId = Guid.NewGuid();
        cipher.Manage = true;
        orgAbility.LimitItemDeletion = true;

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(cipher.OrganizationId.Value)
            .Returns(orgAbility);

        var permissions = await sutProvider.Sut.GetCipherPermissionsAsync(cipher, user);

        Assert.True(permissions.Delete);
        Assert.True(permissions.Restore);
    }

    [Theory, BitAutoData]
    public async Task GetCipherPermissionsAsync_WhenLimitedAndEdit_ReturnsFalsePermissions(
        SutProvider<CipherPermissionsService> sutProvider,
        User user,
        CipherDetails cipher,
        OrganizationAbility orgAbility)
    {
        cipher.UserId = Guid.NewGuid(); // Different user
        cipher.OrganizationId = Guid.NewGuid();
        cipher.Edit = true;
        cipher.Manage = false;
        orgAbility.LimitItemDeletion = true;

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(cipher.OrganizationId.Value)
            .Returns(orgAbility);

        var permissions = await sutProvider.Sut.GetCipherPermissionsAsync(cipher, user);

        Assert.False(permissions.Delete);
        Assert.False(permissions.Restore);
    }

    [Theory, BitAutoData]
    public async Task GetCipherPermissionsAsync_WhenNotLimitedAndEdit_ReturnsTruePermissions(
        SutProvider<CipherPermissionsService> sutProvider,
        User user,
        CipherDetails cipher,
        OrganizationAbility orgAbility)
    {
        cipher.UserId = Guid.NewGuid(); // Different user
        cipher.OrganizationId = Guid.NewGuid();
        cipher.Edit = true;
        cipher.Manage = false;
        orgAbility.LimitItemDeletion = false;

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(cipher.OrganizationId.Value)
            .Returns(orgAbility);

        var permissions = await sutProvider.Sut.GetCipherPermissionsAsync(cipher, user);

        Assert.True(permissions.Delete);
        Assert.True(permissions.Restore);
    }

    [Theory, BitAutoData]
    public async Task GetManyCipherPermissionsAsync_ProcessesAllCiphers(
        SutProvider<CipherPermissionsService> sutProvider,
        User user,
        List<Cipher> ciphers)
    {
        // Set all ciphers to be owned by the user for simplicity
        foreach (var cipher in ciphers)
        {
            cipher.UserId = user.Id;
        }

        var result = await sutProvider.Sut.GetManyCipherPermissionsAsync(ciphers, user);

        Assert.Equal(ciphers.Count, result.Count);
        Assert.All(result.Values, p => Assert.True(p.Delete && p.Restore));
        Assert.All(ciphers, c => Assert.Contains(c.Id, result.Keys));
    }
}
