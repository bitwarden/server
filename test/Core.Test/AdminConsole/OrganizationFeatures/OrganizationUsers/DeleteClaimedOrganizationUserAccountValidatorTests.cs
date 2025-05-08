using Bit.Core.AdminConsole.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Shared.Validation;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Validators;

[SutProviderCustomize]
public class DeleteClaimedOrganizationUserAccountValidatorTests
{
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ShouldExecuteSyncValidatorsFirst(
        Guid organizationId,
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider)
    {
        // Arrange
        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUser = null,
            User = null,
            IsClaimed = true,
            DeletingUserId = Guid.NewGuid()
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync([request]);

        // Assert
        Assert.Single(result.InvalidResults);

        var invalidResult = result.InvalidResults.First();
        Assert.IsType<Invalid<DeleteUserValidationRequest>>(invalidResult);
        Assert.Equal("Member not found.", invalidResult.ErrorMessageString);

        await sutProvider.GetDependency<ICurrentContext>()
            .DidNotReceive()
            .OrganizationOwner(Arg.Any<Guid>()); ;


        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceive()
            .GetCountByOnlyOwnerAsync(Arg.Any<Guid>()); ;

        await sutProvider.GetDependency<IProviderUserRepository>()
            .DidNotReceive()
            .GetCountByOnlyOwnerAsync(Arg.Any<Guid>()); ;
    }


    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ShouldEnsureUserBelongsToOrganization(
        Guid organizationId,
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider)
    {
        // Arrange
        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUser = null,
            User = null,
            IsClaimed = true,
            DeletingUserId = Guid.NewGuid()
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync([request]);

        // Assert
        Assert.Single(result.InvalidResults);

        var invalidResult = result.InvalidResults.First();
        Assert.IsType<Invalid<DeleteUserValidationRequest>>(invalidResult);
        Assert.IsType<RecordNotFoundError<DeleteUserValidationRequest>>(invalidResult.Errors.First());
        Assert.Equal("Member not found.", invalidResult.ErrorMessageString);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ShouldEnsureUserStatusIsNotInvited(
        OrganizationUser orgUser,
        User user,
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider)
    {
        orgUser.Status = OrganizationUserStatusType.Invited;
        orgUser.UserId = user.Id;

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = orgUser.OrganizationId,
            OrganizationUser = orgUser,
            User = user,
            IsClaimed = true,
            DeletingUserId = Guid.NewGuid()
        };

        var result = await sutProvider.Sut.ValidateAsync([request]);

        var invalidResult = Assert.Single(result.InvalidResults);
        Assert.IsType<Invalid<DeleteUserValidationRequest>>(invalidResult);
        Assert.IsType<BadRequestError<DeleteUserValidationRequest>>(invalidResult.Errors.First());
        Assert.Equal("You cannot delete a member with Invited status.", invalidResult.ErrorMessageString);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ShouldPreventSelfDeletion(
        OrganizationUser orgUser,
        User user,
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider)
    {
        orgUser.Status = OrganizationUserStatusType.Confirmed;
        orgUser.UserId = user.Id;

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = orgUser.OrganizationId,
            OrganizationUser = orgUser,
            User = user,
            IsClaimed = true,
            DeletingUserId = user.Id
        };

        var result = await sutProvider.Sut.ValidateAsync([request]);

        var invalidResult = Assert.Single(result.InvalidResults);
        Assert.IsType<Invalid<DeleteUserValidationRequest>>(invalidResult);
        Assert.IsType<BadRequestError<DeleteUserValidationRequest>>(invalidResult.Errors.First());
        Assert.Equal("You cannot delete yourself.", invalidResult.ErrorMessageString);
    }


    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_ShouldEnsureUserIsClaimedByOrganization(
        OrganizationUser orgUser,
        User user,
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider)
    {
        orgUser.Status = OrganizationUserStatusType.Confirmed;
        orgUser.UserId = user.Id;

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = orgUser.OrganizationId,
            OrganizationUser = orgUser,
            User = user,
            IsClaimed = false,
            DeletingUserId = Guid.NewGuid()
        };

        var result = await sutProvider.Sut.ValidateAsync([request]);

        var invalidResult = Assert.Single(result.InvalidResults);
        Assert.IsType<Invalid<DeleteUserValidationRequest>>(invalidResult);
        Assert.IsType<BadRequestError<DeleteUserValidationRequest>>(invalidResult.Errors.First());
        Assert.Equal("Member is not managed by the organization.", invalidResult.ErrorMessageString);
    }


    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_EnsureOnlyOwnersCanDeleteOwnersAsync(
        OrganizationUser orgUser,
        User user,
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider)
    {
        orgUser.Status = OrganizationUserStatusType.Confirmed;
        orgUser.Type = OrganizationUserType.Owner;
        orgUser.UserId = user.Id;

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.OrganizationOwner(orgUser.OrganizationId).Returns(false);

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = orgUser.OrganizationId,
            OrganizationUser = orgUser,
            User = user,
            IsClaimed = true,
            DeletingUserId = Guid.NewGuid()
        };

        var result = await sutProvider.Sut.ValidateAsync([request]);

        var invalidResult = Assert.Single(result.InvalidResults);
        Assert.IsType<Invalid<DeleteUserValidationRequest>>(invalidResult);
        Assert.IsType<BadRequestError<DeleteUserValidationRequest>>(invalidResult.Errors.First());
        Assert.Equal("Only owners can delete other owners.", invalidResult.ErrorMessageString);

        await sutProvider.GetDependency<ICurrentContext>()
            .Received(1)
            .OrganizationOwner(orgUser.OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_EnsureUserIsNotSoleOrganizationOwnerAsync(
        OrganizationUser orgUser,
        User user,
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider)
    {
        orgUser.Status = OrganizationUserStatusType.Confirmed;
        orgUser.Type = OrganizationUserType.Owner;
        orgUser.UserId = user.Id;

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.OrganizationOwner(orgUser.OrganizationId).Returns(true);

        var orgRepo = sutProvider.GetDependency<IOrganizationUserRepository>();
        orgRepo.GetCountByOnlyOwnerAsync(user.Id).Returns(1);

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = orgUser.OrganizationId,
            OrganizationUser = orgUser,
            User = user,
            IsClaimed = true,
            DeletingUserId = Guid.NewGuid()
        };

        var result = await sutProvider.Sut.ValidateAsync([request]);

        var invalidResult = Assert.Single(result.InvalidResults);
        Assert.IsType<Invalid<DeleteUserValidationRequest>>(invalidResult);
        Assert.IsType<BadRequestError<DeleteUserValidationRequest>>(invalidResult.Errors.First());
        Assert.StartsWith("Cannot delete this user because it is the sole owner", invalidResult.ErrorMessageString);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetCountByOnlyOwnerAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_EnsureUserIsNotSoleProviderOwnerAsync(
        OrganizationUser orgUser,
        User user,
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider)
    {
        orgUser.Status = OrganizationUserStatusType.Confirmed;
        orgUser.Type = OrganizationUserType.Owner;
        orgUser.UserId = user.Id;

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.OrganizationOwner(orgUser.OrganizationId).Returns(true);

        var orgRepo = sutProvider.GetDependency<IOrganizationUserRepository>();
        orgRepo.GetCountByOnlyOwnerAsync(user.Id).Returns(0);

        var providerRepo = sutProvider.GetDependency<IProviderUserRepository>();
        providerRepo.GetCountByOnlyOwnerAsync(user.Id).Returns(1);

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = orgUser.OrganizationId,
            OrganizationUser = orgUser,
            User = user,
            IsClaimed = true,
            DeletingUserId = Guid.NewGuid()
        };

        var result = await sutProvider.Sut.ValidateAsync([request]);

        var invalidResult = Assert.Single(result.InvalidResults);
        Assert.IsType<Invalid<DeleteUserValidationRequest>>(invalidResult);
        Assert.IsType<BadRequestError<DeleteUserValidationRequest>>(invalidResult.Errors.First());
        Assert.StartsWith("Cannot delete this user because it is the sole owner", invalidResult.ErrorMessageString);

        await sutProvider.GetDependency<IProviderUserRepository>()
            .Received(1)
            .GetCountByOnlyOwnerAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_EnsureCustomUsersCannotDeleteAdminsAsync(
        OrganizationUser orgUser,
        User user,
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider)
    {
        orgUser.Status = OrganizationUserStatusType.Confirmed;
        orgUser.Type = OrganizationUserType.Admin;
        orgUser.UserId = user.Id;

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.OrganizationCustom(orgUser.OrganizationId).Returns(true);

        var orgRepo = sutProvider.GetDependency<IOrganizationUserRepository>();
        orgRepo.GetCountByOnlyOwnerAsync(user.Id).Returns(0);

        var providerRepo = sutProvider.GetDependency<IProviderUserRepository>();
        providerRepo.GetCountByOnlyOwnerAsync(user.Id).Returns(0);

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = orgUser.OrganizationId,
            OrganizationUser = orgUser,
            User = user,
            IsClaimed = true,
            DeletingUserId = Guid.NewGuid()
        };

        var result = await sutProvider.Sut.ValidateAsync([request]);

        var invalidResult = Assert.Single(result.InvalidResults);
        Assert.IsType<Invalid<DeleteUserValidationRequest>>(invalidResult);
        Assert.IsType<BadRequestError<DeleteUserValidationRequest>>(invalidResult.Errors.First());
        Assert.Equal("Custom users can not delete admins.", invalidResult.ErrorMessageString);

        await sutProvider.GetDependency<ICurrentContext>()
            .Received(1)
            .OrganizationCustom(orgUser.OrganizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithMixedOfSuccessesAndFailures(
     OrganizationUser validOrgUser,
     User validUser,
     OrganizationUser invalidOrgUser,
     User invalidUser,
     SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider)
    {
        // Arrange - valid user
        validOrgUser.Status = OrganizationUserStatusType.Confirmed;
        validOrgUser.Type = OrganizationUserType.Admin;
        validOrgUser.UserId = validUser.Id;

        var context = sutProvider.GetDependency<ICurrentContext>();
        context.OrganizationOwner(validOrgUser.OrganizationId).Returns(true);
        context.OrganizationCustom(validOrgUser.OrganizationId).Returns(false);

        var orgRepo = sutProvider.GetDependency<IOrganizationUserRepository>();
        orgRepo.GetCountByOnlyOwnerAsync(validUser.Id).Returns(0);

        var providerRepo = sutProvider.GetDependency<IProviderUserRepository>();
        providerRepo.GetCountByOnlyOwnerAsync(validUser.Id).Returns(0);

        var validRequest = new DeleteUserValidationRequest
        {
            OrganizationId = validOrgUser.OrganizationId,
            OrganizationUser = validOrgUser,
            User = validUser,
            IsClaimed = true,
            DeletingUserId = Guid.NewGuid()
        };

        // Arrange - invalid user (unclaimed account)
        invalidOrgUser.Status = OrganizationUserStatusType.Confirmed;
        invalidOrgUser.Type = OrganizationUserType.Admin;
        invalidOrgUser.UserId = invalidUser.Id;

        var invalidRequest = new DeleteUserValidationRequest
        {
            OrganizationId = invalidOrgUser.OrganizationId,
            OrganizationUser = invalidOrgUser,
            User = invalidUser,
            IsClaimed = false,
            DeletingUserId = Guid.NewGuid()
        };

        // Act
        var result = await sutProvider.Sut.ValidateAsync([validRequest, invalidRequest]);

        // Assert
        Assert.Single(result.ValidResults);
        Assert.Single(result.InvalidResults);

        var invalid = result.InvalidResults.First();
        Assert.IsType<Invalid<DeleteUserValidationRequest>>(invalid);
        Assert.IsType<BadRequestError<DeleteUserValidationRequest>>(invalid.Errors.First());
        Assert.Equal("Member is not managed by the organization.", invalid.ErrorMessageString);
    }

}
