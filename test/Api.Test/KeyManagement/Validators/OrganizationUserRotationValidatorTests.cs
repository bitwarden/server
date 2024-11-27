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
}
