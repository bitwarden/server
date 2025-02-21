using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Vault.Authorization.Permissions;
using Bit.Core.Vault.Models.Data;
using Xunit;

namespace Bit.Core.Test.Vault.Authorization.Permissions;

public class NormalCipherPermissionTests
{
    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public void CanRestore_WhenCipherIsOwnedByOrganization(
        bool limitItemDeletion, bool manage, bool edit, bool expectedResult)
    {
        // Arrange
        var user = new User { Id = Guid.Empty };
        var cipherDetails = new CipherDetails { Manage = manage, Edit = edit, UserId = null };
        var organizationAbility = new OrganizationAbility { LimitItemDeletion = limitItemDeletion };

        // Act
        var result = NormalCipherPermissions.CanRestore(user, cipherDetails, organizationAbility);

        // Assert
        Assert.Equal(result, expectedResult);
    }

    [Fact]
    public void CanRestore_WhenCipherIsOwnedByUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        var cipherDetails = new CipherDetails { UserId = userId };
        var organizationAbility = new OrganizationAbility { };

        // Act
        var result = NormalCipherPermissions.CanRestore(user, cipherDetails, organizationAbility);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanRestore_WhenCipherHasNoOwner_ShouldThrowException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        var cipherDetails = new CipherDetails { UserId = null };


        // Act
        // Assert
        Assert.Throws<Exception>(() => NormalCipherPermissions.CanRestore(user, cipherDetails, null));
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public void CanDelete_WhenCipherIsOwnedByOrganization(
        bool limitItemDeletion, bool manage, bool edit, bool expectedResult)
    {
        // Arrange
        var user = new User { Id = Guid.Empty };
        var cipherDetails = new CipherDetails { Manage = manage, Edit = edit, UserId = null };
        var organizationAbility = new OrganizationAbility { LimitItemDeletion = limitItemDeletion };

        // Act
        var result = NormalCipherPermissions.CanRestore(user, cipherDetails, organizationAbility);

        // Assert
        Assert.Equal(result, expectedResult);
    }

    [Fact]
    public void CanDelete_WhenCipherIsOwnedByUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        var cipherDetails = new CipherDetails { UserId = userId };
        var organizationAbility = new OrganizationAbility { };

        // Act
        var result = NormalCipherPermissions.CanDelete(user, cipherDetails, organizationAbility);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanDelete_WhenCipherHasNoOwner_ShouldThrowException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        var cipherDetails = new CipherDetails { UserId = null };


        // Act
        // Assert
        Assert.Throws<Exception>(() => NormalCipherPermissions.CanDelete(user, cipherDetails, null));
    }
}
