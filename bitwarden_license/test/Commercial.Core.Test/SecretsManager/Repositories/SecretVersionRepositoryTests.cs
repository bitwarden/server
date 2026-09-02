using Bit.Core.SecretsManager.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Repositories;

public class SecretVersionRepositoryTests
{
    [Theory]
    [BitAutoData]
    public void SecretVersion_EntityCreation_Success(SecretVersion secretVersion)
    {
        // Arrange & Act
        secretVersion.SetNewId();

        // Assert
        Assert.NotEqual(Guid.Empty, secretVersion.Id);
        Assert.NotEqual(Guid.Empty, secretVersion.SecretId);
        Assert.NotNull(secretVersion.Value);
        Assert.NotEqual(default, secretVersion.VersionDate);
    }

    [Theory]
    [BitAutoData]
    public void SecretVersion_WithServiceAccountEditor_Success(SecretVersion secretVersion, Guid serviceAccountId)
    {
        // Arrange & Act
        secretVersion.EditorServiceAccountId = serviceAccountId;
        secretVersion.EditorOrganizationUserId = null;

        // Assert
        Assert.Equal(serviceAccountId, secretVersion.EditorServiceAccountId);
        Assert.Null(secretVersion.EditorOrganizationUserId);
    }

    [Theory]
    [BitAutoData]
    public void SecretVersion_WithOrganizationUserEditor_Success(SecretVersion secretVersion, Guid organizationUserId)
    {
        // Arrange & Act
        secretVersion.EditorOrganizationUserId = organizationUserId;
        secretVersion.EditorServiceAccountId = null;

        // Assert
        Assert.Equal(organizationUserId, secretVersion.EditorOrganizationUserId);
        Assert.Null(secretVersion.EditorServiceAccountId);
    }

    [Theory]
    [BitAutoData]
    public void SecretVersion_NullableEditors_Success(SecretVersion secretVersion)
    {
        // Arrange & Act
        secretVersion.EditorServiceAccountId = null;
        secretVersion.EditorOrganizationUserId = null;

        // Assert
        Assert.Null(secretVersion.EditorServiceAccountId);
        Assert.Null(secretVersion.EditorOrganizationUserId);
    }

    [Theory]
    [BitAutoData]
    public void SecretVersion_VersionDateSet_Success(SecretVersion secretVersion)
    {
        // Arrange
        var versionDate = DateTime.UtcNow;

        // Act
        secretVersion.VersionDate = versionDate;

        // Assert
        Assert.Equal(versionDate, secretVersion.VersionDate);
    }

    [Theory]
    [BitAutoData]
    public void SecretVersion_ValueEncrypted_Success(SecretVersion secretVersion, string encryptedValue)
    {
        // Arrange & Act
        secretVersion.Value = encryptedValue;

        // Assert
        Assert.Equal(encryptedValue, secretVersion.Value);
        Assert.NotEmpty(secretVersion.Value);
    }

    [Theory]
    [BitAutoData]
    public void SecretVersion_MultipleVersions_DifferentIds(List<SecretVersion> secretVersions, Guid secretId)
    {
        // Arrange & Act
        foreach (var version in secretVersions)
        {
            version.SecretId = secretId;
            version.SetNewId();
        }

        // Assert
        var distinctIds = secretVersions.Select(v => v.Id).Distinct();
        Assert.Equal(secretVersions.Count, distinctIds.Count());
        Assert.All(secretVersions, v => Assert.Equal(secretId, v.SecretId));
    }

    [Theory]
    [BitAutoData]
    public void SecretVersion_VersionDateOrdering_Success(SecretVersion version1, SecretVersion version2, SecretVersion version3, Guid secretId)
    {
        // Arrange
        var now = DateTime.UtcNow;
        version1.SecretId = secretId;
        version1.VersionDate = now.AddDays(-2);

        version2.SecretId = secretId;
        version2.VersionDate = now.AddDays(-1);

        version3.SecretId = secretId;
        version3.VersionDate = now;

        var versions = new List<SecretVersion> { version2, version3, version1 };

        // Act
        var orderedVersions = versions.OrderByDescending(v => v.VersionDate).ToList();

        // Assert
        Assert.Equal(version3.Id, orderedVersions[0].Id); // Most recent
        Assert.Equal(version2.Id, orderedVersions[1].Id);
        Assert.Equal(version1.Id, orderedVersions[2].Id); // Oldest
    }
}
