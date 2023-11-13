using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Validators;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Validators;

[SutProviderCustomize]
public class ResetPasswordRotationValidatorTests
{
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_Success_ReturnsValid(
        SutProvider<ResetPasswordRotationValidator> sutProvider, User user,
        IEnumerable<ResetPasswordWithIdRequestModel> resetPasswordKeys)
    {
        var existingUserResetPassword = resetPasswordKeys
            .Select(a =>
                new OrganizationUser
                {
                    Id = new Guid(), ResetPasswordKey = a.ResetPasswordKey, OrganizationId = a.OrganizationId
                }).ToList();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserResetPassword);

        var result = await sutProvider.Sut.ValidateAsync(user, resetPasswordKeys);

        Assert.Equal(result.Select(r => r.OrganizationId), resetPasswordKeys.Select(a => a.OrganizationId));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_MissingResetPassword_Throws(
        SutProvider<ResetPasswordRotationValidator> sutProvider, User user,
        IEnumerable<ResetPasswordWithIdRequestModel> resetPasswordKeys)
    {
        var existingUserResetPassword = resetPasswordKeys
            .Select(a =>
                new OrganizationUser
                {
                    Id = new Guid(), ResetPasswordKey = a.ResetPasswordKey, OrganizationId = a.OrganizationId
                }).ToList();
        existingUserResetPassword.Add(new OrganizationUser
        {
            Id = Guid.NewGuid(), ResetPasswordKey = "Missing ResetPasswordKey"
        });
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserResetPassword);


        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user, resetPasswordKeys));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ResetPasswordDoesNotBelongToUser_NotReturned(
        SutProvider<ResetPasswordRotationValidator> sutProvider, User user,
        IEnumerable<ResetPasswordWithIdRequestModel> resetPasswordKeys)
    {
        var existingUserResetPassword = resetPasswordKeys
            .Select(a =>
                new OrganizationUser
                {
                    Id = new Guid(), ResetPasswordKey = a.ResetPasswordKey, OrganizationId = a.OrganizationId
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
        SutProvider<ResetPasswordRotationValidator> sutProvider, User user,
        IEnumerable<ResetPasswordWithIdRequestModel> resetPasswordKeys)
    {
        var existingUserResetPassword = resetPasswordKeys
            .Select(a =>
                new OrganizationUser
                {
                    Id = new Guid(), ResetPasswordKey = a.ResetPasswordKey, OrganizationId = a.OrganizationId
                }).ToList();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyByUserAsync(user.Id)
            .Returns(existingUserResetPassword);
        resetPasswordKeys.First().ResetPasswordKey = null;

        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.ValidateAsync(user, resetPasswordKeys));
    }
}
