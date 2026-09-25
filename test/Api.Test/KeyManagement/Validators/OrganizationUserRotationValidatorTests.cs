using Bit.Api.KeyManagement.Models.Requests;
using Bit.Api.KeyManagement.Validators;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Validators;

[SutProviderCustomize]
public class OrganizationUserRotationValidatorTests
{
    private const string _v1PrivateKey = "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";
    private const string _v2PrivateKey = "7.AOs41Hd8OQiCPXjyJKCiDA==";

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_Success_ReturnsValid(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        IEnumerable<OrganizationUserAccountRecoveryRequestModel> accountRecoveryKeys)
    {
        var existingUserResetPassword = accountRecoveryKeys
            .Select(a =>
                new OrganizationUser
                {
                    Id = new Guid(),
                    ResetPasswordKey = a.ResetPasswordKey,
                    OrganizationId = a.OrganizationId
                }).ToList();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserResetPassword);

        var result = await sutProvider.Sut.ValidateAsync(user, Rotation(accountRecoveryKeys));

        Assert.Equal(result.Select(r => r.OrganizationId), accountRecoveryKeys.Select(a => a.OrganizationId));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_NullAccountRecoveryKeys_ReturnsEmptyList(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user)
    {
        // Arrange
        IEnumerable<OrganizationUserAccountRecoveryRequestModel> accountRecoveryKeys = null;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, Rotation(accountRecoveryKeys));

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_NoOrgUsers_ReturnsEmptyList(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        IEnumerable<OrganizationUserAccountRecoveryRequestModel> accountRecoveryKeys)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(new List<OrganizationUser>()); // Return an empty list

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, Rotation(accountRecoveryKeys));

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData([null])]
    [BitAutoData("")]
    public async Task ValidateAsync_OrgUsersWithNullOrEmptyResetPasswordKey_FiltersOutInvalidKeys(
        string? invalidResetPasswordKey,
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        OrganizationUserAccountRecoveryRequestModel validAccountRecoveryKey)
    {
        // Arrange
        var existingUserResetPassword = new List<OrganizationUser>
        {
            // Valid org user with reset password key
            new OrganizationUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = validAccountRecoveryKey.OrganizationId,
                ResetPasswordKey = validAccountRecoveryKey.ResetPasswordKey
            },
            // Invalid org user with null or empty reset password key - should be filtered out
            new OrganizationUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = Guid.NewGuid(),
                ResetPasswordKey = invalidResetPasswordKey
            }
        };
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserResetPassword);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, Rotation([validAccountRecoveryKey]));

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(validAccountRecoveryKey.OrganizationId, result[0].OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_MissingAccountRecoveryKey_Throws(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        IEnumerable<OrganizationUserAccountRecoveryRequestModel> accountRecoveryKeys)
    {
        var existingUserResetPassword = accountRecoveryKeys
            .Select(a =>
                new OrganizationUser
                {
                    Id = new Guid(),
                    ResetPasswordKey = a.ResetPasswordKey,
                    OrganizationId = a.OrganizationId
                }).ToList();
        existingUserResetPassword.Add(new OrganizationUser
        {
            Id = Guid.NewGuid(),
            ResetPasswordKey = "Missing ResetPasswordKey"
        });
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserResetPassword);


        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user, Rotation(accountRecoveryKeys)));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_AccountRecoveryKeyDoesNotBelongToUser_NotReturned(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        IEnumerable<OrganizationUserAccountRecoveryRequestModel> accountRecoveryKeys)
    {
        var existingUserResetPassword = accountRecoveryKeys
            .Select(a =>
                new OrganizationUser
                {
                    Id = new Guid(),
                    ResetPasswordKey = a.ResetPasswordKey,
                    OrganizationId = a.OrganizationId
                }).ToList();
        existingUserResetPassword.RemoveAt(0);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserResetPassword);

        var result = await sutProvider.Sut.ValidateAsync(user, Rotation(accountRecoveryKeys));

        Assert.DoesNotContain(result, c => c.Id == accountRecoveryKeys.First().OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_AttemptToSetKeyToNull_Throws(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        IEnumerable<OrganizationUserAccountRecoveryRequestModel> accountRecoveryKeys)
    {
        var existingUserResetPassword = accountRecoveryKeys
            .Select(a =>
                new OrganizationUser
                {
                    Id = new Guid(),
                    ResetPasswordKey = a.ResetPasswordKey,
                    OrganizationId = a.OrganizationId
                }).ToList();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserResetPassword);
        accountRecoveryKeys.First().ResetPasswordKey = null;

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user, Rotation(accountRecoveryKeys)));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_NoOrganizationsInRequestButInDatabase_Throws(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        IEnumerable<OrganizationUserAccountRecoveryRequestModel> accountRecoveryKeys)
    {
        var existingUserResetPassword = accountRecoveryKeys
            .Select(a =>
                new OrganizationUser
                {
                    Id = new Guid(),
                    ResetPasswordKey = a.ResetPasswordKey,
                    OrganizationId = a.OrganizationId
                }).ToList();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserResetPassword);

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user,
                Rotation(Enumerable.Empty<OrganizationUserAccountRecoveryRequestModel>())));
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData(" ")]
    public async Task ValidateAsync_EmptyOrWhitespaceKey_Throws(
        string emptyKey,
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        OrganizationUserAccountRecoveryRequestModel validAccountRecoveryKey)
    {
        // Arrange
        var existingUserResetPassword = new List<OrganizationUser>
        {
            new OrganizationUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = validAccountRecoveryKey.OrganizationId,
                ResetPasswordKey = "existing-valid-key"
            }
        };
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserResetPassword);

        validAccountRecoveryKey.ResetPasswordKey = emptyKey;

        // Act & Assert - An empty key leaves the organization unable to recover the account
        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user, Rotation([validAccountRecoveryKey])));
    }

    [Theory]
    [BitAutoData(" ")]
    public async Task ValidateAsync_WhitespaceOnlyExistingKey_FiltersOut(
        string whitespaceKey,
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        OrganizationUserAccountRecoveryRequestModel validAccountRecoveryKey)
    {
        // Arrange
        var existingUserResetPassword = new List<OrganizationUser>
        {
            new OrganizationUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = validAccountRecoveryKey.OrganizationId,
                ResetPasswordKey = validAccountRecoveryKey.ResetPasswordKey
            },
            // Whitespace-only key should be filtered out
            new OrganizationUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = Guid.NewGuid(),
                ResetPasswordKey = whitespaceKey
            }
        };
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserResetPassword);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, Rotation([validAccountRecoveryKey]));

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(validAccountRecoveryKey.OrganizationId, result[0].OrganizationId);
    }

    // An upgrade rotation treats a missing or empty key as "none sent" rather than a violation, so a client that
    // sends one keeps its stored key instead of having the whole rotation refused.
    [Theory]
    [BitAutoData([null])]
    [BitAutoData("")]
    [BitAutoData(" ")]
    public async Task ValidateAsync_V2UpgradeTokenWithoutAccountRecoveryKey_KeepsStoredKey(
        string? missingKey,
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        OrganizationUserAccountRecoveryRequestModel accountRecoveryKey)
    {
        // Arrange
        user.PrivateKey = _v1PrivateKey;
        var existing = SetupEnrolledMembership(sutProvider, user, accountRecoveryKey.OrganizationId);
        accountRecoveryKey.ResetPasswordKey = missingKey;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user,
            Rotation([accountRecoveryKey], hasV2UpgradeToken: true));

        // Assert - The stored key survives, so the organization keeps account recovery for this member
        Assert.Single(result);
        Assert.Equal(existing.ResetPasswordKey, result[0].ResetPasswordKey);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_V2UpgradeTokenWithKey_Throws(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        OrganizationUserAccountRecoveryRequestModel accountRecoveryKey)
    {
        // Arrange
        user.PrivateKey = _v1PrivateKey;
        SetupEnrolledMembership(sutProvider, user, accountRecoveryKey.OrganizationId);

        // Act & Assert - A client cannot re-encapsulate the key during an upgrade, so sending one is a bug
        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user,
                Rotation([accountRecoveryKey], hasV2UpgradeToken: true)));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_V2UpgradeTokenWithMissingOrganization_Throws(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user)
    {
        // Arrange
        user.PrivateKey = _v1PrivateKey;
        SetupEnrolledMembership(sutProvider, user, Guid.NewGuid());

        // Act & Assert - An enrolled organization must still be included, so it cannot be silently skipped
        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user,
                Rotation(Enumerable.Empty<OrganizationUserAccountRecoveryRequestModel>(),
                    hasV2UpgradeToken: true)));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_V2UserWithV2UpgradeTokenAndNullKey_Throws(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        OrganizationUserAccountRecoveryRequestModel accountRecoveryKey)
    {
        // Arrange - The command discards a V2 user's token, so their key must rotate as usual
        user.PrivateKey = _v2PrivateKey;
        SetupEnrolledMembership(sutProvider, user, accountRecoveryKey.OrganizationId);
        accountRecoveryKey.ResetPasswordKey = null;

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user,
                Rotation([accountRecoveryKey], hasV2UpgradeToken: true)));
    }

    private static OrganizationUser SetupEnrolledMembership(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user, Guid organizationId)
    {
        var existing = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ResetPasswordKey = "4.stored-account-recovery-key"
        };
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(new List<OrganizationUser> { existing });
        return existing;
    }

    private static OrganizationAccountRecoveryRotationData Rotation(
        IEnumerable<OrganizationUserAccountRecoveryRequestModel> accountRecoveryKeys,
        bool hasV2UpgradeToken = false)
    {
        return new OrganizationAccountRecoveryRotationData
        {
            AccountRecoveryUnlockData = accountRecoveryKeys,
            HasV2UpgradeToken = hasV2UpgradeToken
        };
    }
}
