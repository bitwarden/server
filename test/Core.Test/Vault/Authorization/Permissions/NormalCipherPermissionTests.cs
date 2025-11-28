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
        var organizationId = Guid.NewGuid();
        var cipherDetails = new CipherDetails { Manage = manage, Edit = edit, UserId = null, OrganizationId = organizationId };
        var organizationAbility = new OrganizationAbility { Id = organizationId, LimitItemDeletion = limitItemDeletion };

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
        var user = new User { Id = Guid.NewGuid() };
        var cipherDetails = new CipherDetails { UserId = null };


        // Act
        // Assert
        Assert.Throws<Exception>(() => NormalCipherPermissions.CanRestore(user, cipherDetails, null));
    }

    public static List<object[]> TestCases =>
    [
        new object[] { new OrganizationAbility { Id = Guid.Empty } },
        new object[] { null },
    ];

    [Theory]
    [MemberData(nameof(TestCases))]
    public void CanRestore_WhenCipherDoesNotBelongToInputOrganization_ShouldThrowException(OrganizationAbility? organizationAbility)
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid() };
        var cipherDetails = new CipherDetails { UserId = null, OrganizationId = Guid.NewGuid() };

        // Act
        var exception = Assert.Throws<Exception>(() => NormalCipherPermissions.CanDelete(user, cipherDetails, organizationAbility));

        // Assert
        Assert.Equal("Cipher does not belong to the input organization.", exception.Message);
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
        var organizationId = Guid.NewGuid();
        var cipherDetails = new CipherDetails { Manage = manage, Edit = edit, UserId = null, OrganizationId = organizationId };
        var organizationAbility = new OrganizationAbility { Id = organizationId, LimitItemDeletion = limitItemDeletion };

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
        var user = new User { Id = Guid.NewGuid() };
        var cipherDetails = new CipherDetails { UserId = null };


        // Act
        var exception = Assert.Throws<Exception>(() => NormalCipherPermissions.CanDelete(user, cipherDetails, null));

        // Assert
        Assert.Equal("Cipher needs to belong to a user or an organization.", exception.Message);
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void CanDelete_WhenCipherDoesNotBelongToInputOrganization_ShouldThrowException(OrganizationAbility? organizationAbility)
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid() };
        var cipherDetails = new CipherDetails { UserId = null, OrganizationId = Guid.NewGuid() };

        // Act
        var exception = Assert.Throws<Exception>(() => NormalCipherPermissions.CanDelete(user, cipherDetails, organizationAbility));

        // Assert
        Assert.Equal("Cipher does not belong to the input organization.", exception.Message);
    }
}
