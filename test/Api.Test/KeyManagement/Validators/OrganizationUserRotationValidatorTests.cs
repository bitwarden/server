using Bit.Api.AdminConsole.Models.Request.Organizations;
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
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_Success_ReturnsValid(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        IEnumerable<ResetPasswordWithOrgIdRequestModel> resetPasswordKeys)
    {
        var existingUserResetPassword = resetPasswordKeys
            .Select(a =>
                new OrganizationUser
                {
                    Id = new Guid(),
                    ResetPasswordKey = a.ResetPasswordKey,
                    OrganizationId = a.OrganizationId
                }).ToList();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserResetPassword);

        var result = await sutProvider.Sut.ValidateAsync(user, resetPasswordKeys);

        Assert.Equal(result.Select(r => r.OrganizationId), resetPasswordKeys.Select(a => a.OrganizationId));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_NullResetPasswordKeys_ReturnsEmptyList(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user)
    {
        // Arrange
        IEnumerable<ResetPasswordWithOrgIdRequestModel> resetPasswordKeys = null;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, resetPasswordKeys);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_NoOrgUsers_ReturnsEmptyList(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        IEnumerable<ResetPasswordWithOrgIdRequestModel> resetPasswordKeys)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(new List<OrganizationUser>()); // Return an empty list

        // Act
        var result = await sutProvider.Sut.ValidateAsync(user, resetPasswordKeys);

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
        ResetPasswordWithOrgIdRequestModel validResetPasswordKey)
    {
        // Arrange
        var existingUserResetPassword = new List<OrganizationUser>
        {
            // Valid org user with reset password key
            new OrganizationUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = validResetPasswordKey.OrganizationId,
                ResetPasswordKey = validResetPasswordKey.ResetPasswordKey
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
        var result = await sutProvider.Sut.ValidateAsync(user, new[] { validResetPasswordKey });

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(validResetPasswordKey.OrganizationId, result[0].OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_MissingResetPassword_Throws(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        IEnumerable<ResetPasswordWithOrgIdRequestModel> resetPasswordKeys)
    {
        var existingUserResetPassword = resetPasswordKeys
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
            await sutProvider.Sut.ValidateAsync(user, resetPasswordKeys));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ResetPasswordDoesNotBelongToUser_NotReturned(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        IEnumerable<ResetPasswordWithOrgIdRequestModel> resetPasswordKeys)
    {
        var existingUserResetPassword = resetPasswordKeys
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

        var result = await sutProvider.Sut.ValidateAsync(user, resetPasswordKeys);

        Assert.DoesNotContain(result, c => c.Id == resetPasswordKeys.First().OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_AttemptToSetKeyToNull_Throws(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        IEnumerable<ResetPasswordWithOrgIdRequestModel> resetPasswordKeys)
    {
        var existingUserResetPassword = resetPasswordKeys
            .Select(a =>
                new OrganizationUser
                {
                    Id = new Guid(),
                    ResetPasswordKey = a.ResetPasswordKey,
                    OrganizationId = a.OrganizationId
                }).ToList();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserResetPassword);
        resetPasswordKeys.First().ResetPasswordKey = null;

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user, resetPasswordKeys));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_NoOrganizationsInRequestButInDatabase_Throws(
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        IEnumerable<ResetPasswordWithOrgIdRequestModel> resetPasswordKeys)
    {
        var existingUserResetPassword = resetPasswordKeys
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
            await sutProvider.Sut.ValidateAsync(user, Enumerable.Empty<ResetPasswordWithOrgIdRequestModel>()));
    }

    // TODO: Remove this test after https://bitwarden.atlassian.net/browse/PM-31001 is resolved.
    // Clients currently send "" as a reset password key value during rotation due to a client-side bug.
    // The server must accept "" to avoid blocking key rotation for affected users.
    // After PM-31001 is fixed, this should be replaced with a test asserting that "" throws BadRequestException.
    [Theory]
    [BitAutoData("")]
    [BitAutoData(" ")]
    public async Task ValidateAsync_EmptyOrWhitespaceKey_AcceptedDueToClientBug(
        string emptyKey,
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        ResetPasswordWithOrgIdRequestModel validResetPasswordKey)
    {
        // Arrange
        var existingUserResetPassword = new List<OrganizationUser>
        {
            new OrganizationUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = validResetPasswordKey.OrganizationId,
                ResetPasswordKey = "existing-valid-key"
            }
        };
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserResetPassword);

        // Set the incoming key to empty/whitespace (simulating client bug)
        validResetPasswordKey.ResetPasswordKey = emptyKey;

        // Act — rotation should succeed (not throw) to preserve backward compatibility
        var result = await sutProvider.Sut.ValidateAsync(user, new[] { validResetPasswordKey });

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(emptyKey, result[0].ResetPasswordKey);
    }

    [Theory]
    [BitAutoData(" ")]
    public async Task ValidateAsync_WhitespaceOnlyExistingKey_FiltersOut(
        string whitespaceKey,
        SutProvider<OrganizationUserRotationValidator> sutProvider, User user,
        ResetPasswordWithOrgIdRequestModel validResetPasswordKey)
    {
        // Arrange
        var existingUserResetPassword = new List<OrganizationUser>
        {
            new OrganizationUser
            {
                Id = Guid.NewGuid(),
                OrganizationId = validResetPasswordKey.OrganizationId,
                ResetPasswordKey = validResetPasswordKey.ResetPasswordKey
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
        var result = await sutProvider.Sut.ValidateAsync(user, new[] { validResetPasswordKey });

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(validResetPasswordKey.OrganizationId, result[0].OrganizationId);
    }
}
