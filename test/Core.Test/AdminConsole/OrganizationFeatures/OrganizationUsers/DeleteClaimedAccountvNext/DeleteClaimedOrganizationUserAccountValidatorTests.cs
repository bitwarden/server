using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccountvNext;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccountvNext;

[SutProviderCustomize]
public class DeleteClaimedOrganizationUserAccountValidatorTests
{
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithValidSingleRequest_ReturnsValidResult(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        User user,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser] OrganizationUser organizationUser)
    {
        organizationUser.UserId = user.Id;
        organizationUser.OrganizationId = organizationId;

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = organizationUser.Id,
            OrganizationUser = organizationUser,
            User = user,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        SetupMocks(sutProvider, organizationId, user.Id);

        var results = await sutProvider.Sut.ValidateAsync([request]);

        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.True(resultsList[0].IsValid);
        Assert.Equal(request, resultsList[0].Request);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithMultipleValidRequests_ReturnsAllValidResults(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        User user1,
        User user2,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser2)
    {
        orgUser1.UserId = user1.Id;
        orgUser1.OrganizationId = organizationId;

        orgUser2.UserId = user2.Id;
        orgUser2.OrganizationId = organizationId;

        var request1 = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = orgUser1.Id,
            OrganizationUser = orgUser1,
            User = user1,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        var request2 = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = orgUser2.Id,
            OrganizationUser = orgUser2,
            User = user2,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        SetupMocks(sutProvider, organizationId, user1.Id);
        SetupMocks(sutProvider, organizationId, user2.Id);

        var results = await sutProvider.Sut.ValidateAsync([request1, request2]);

        var resultsList = results.ToList();
        Assert.Equal(2, resultsList.Count);
        Assert.All(resultsList, result => Assert.True(result.IsValid));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithNullUser_ReturnsUserNotFoundError(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser] OrganizationUser organizationUser)
    {
        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = organizationUser.Id,
            OrganizationUser = organizationUser,
            User = null,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        var results = await sutProvider.Sut.ValidateAsync([request]);

        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.True(resultsList[0].IsError);
        Assert.IsType<UserNotFoundError>(resultsList[0].AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithNullOrganizationUser_ReturnsUserNotFoundError(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        User user,
        Guid organizationId,
        Guid deletingUserId)
    {
        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = Guid.NewGuid(),
            OrganizationUser = null,
            User = user,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        var results = await sutProvider.Sut.ValidateAsync([request]);

        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.True(resultsList[0].IsError);
        Assert.IsType<UserNotFoundError>(resultsList[0].AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithInvitedUser_ReturnsInvalidUserStatusError(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        User user,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser(OrganizationUserStatusType.Invited)] OrganizationUser organizationUser)
    {
        organizationUser.UserId = user.Id;

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = organizationUser.Id,
            OrganizationUser = organizationUser,
            User = user,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        var results = await sutProvider.Sut.ValidateAsync([request]);

        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.True(resultsList[0].IsError);
        Assert.IsType<InvalidUserStatusError>(resultsList[0].AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenDeletingYourself_ReturnsCannotDeleteYourselfError(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        User user,
        Guid organizationId,
        [OrganizationUser] OrganizationUser organizationUser)
    {
        organizationUser.UserId = user.Id;

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = organizationUser.Id,
            OrganizationUser = organizationUser,
            User = user,
            DeletingUserId = user.Id,
            IsClaimed = true
        };

        var results = await sutProvider.Sut.ValidateAsync([request]);

        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.True(resultsList[0].IsError);
        Assert.IsType<CannotDeleteYourselfError>(resultsList[0].AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithUnclaimedUser_ReturnsUserNotClaimedError(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        User user,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser] OrganizationUser organizationUser)
    {
        organizationUser.UserId = user.Id;

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = organizationUser.Id,
            OrganizationUser = organizationUser,
            User = user,
            DeletingUserId = deletingUserId,
            IsClaimed = false
        };

        var results = await sutProvider.Sut.ValidateAsync([request]);

        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.True(resultsList[0].IsError);
        Assert.IsType<UserNotClaimedError>(resultsList[0].AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_DeletingOwnerWhenCurrentUserIsNotOwner_ReturnsCannotDeleteOwnersError(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        User user,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser organizationUser)
    {
        organizationUser.UserId = user.Id;

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = organizationUser.Id,
            OrganizationUser = organizationUser,
            User = user,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        SetupMocks(sutProvider, organizationId, user.Id, OrganizationUserType.Admin);

        var results = await sutProvider.Sut.ValidateAsync([request]);

        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.True(resultsList[0].IsError);
        Assert.IsType<CannotDeleteOwnersError>(resultsList[0].AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_DeletingOwnerWhenCurrentUserIsOwner_ReturnsValidResult(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        User user,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser organizationUser)
    {
        organizationUser.UserId = user.Id;

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = organizationUser.Id,
            OrganizationUser = organizationUser,
            User = user,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        SetupMocks(sutProvider, organizationId, user.Id);

        var results = await sutProvider.Sut.ValidateAsync([request]);

        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.True(resultsList[0].IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithSoleOwnerOfOrganization_ReturnsSoleOwnerError(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        User user,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser] OrganizationUser organizationUser)
    {
        organizationUser.UserId = user.Id;

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = organizationUser.Id,
            OrganizationUser = organizationUser,
            User = user,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        SetupMocks(sutProvider, organizationId, user.Id);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByOnlyOwnerAsync(user.Id)
            .Returns(1);

        var results = await sutProvider.Sut.ValidateAsync([request]);

        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.True(resultsList[0].IsError);
        Assert.IsType<SoleOwnerError>(resultsList[0].AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithSoleProviderOwner_ReturnsSoleProviderError(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        User user,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser] OrganizationUser organizationUser)
    {
        organizationUser.UserId = user.Id;

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = organizationUser.Id,
            OrganizationUser = organizationUser,
            User = user,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        SetupMocks(sutProvider, organizationId, user.Id);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetCountByOnlyOwnerAsync(user.Id)
            .Returns(1);

        var results = await sutProvider.Sut.ValidateAsync([request]);

        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.True(resultsList[0].IsError);
        Assert.IsType<SoleProviderError>(resultsList[0].AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_CustomUserDeletingAdmin_ReturnsCannotDeleteAdminsError(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        User user,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Admin)] OrganizationUser organizationUser)
    {
        organizationUser.UserId = user.Id;

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = organizationUser.Id,
            OrganizationUser = organizationUser,
            User = user,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        SetupMocks(sutProvider, organizationId, user.Id, OrganizationUserType.Custom);

        var results = await sutProvider.Sut.ValidateAsync([request]);

        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.True(resultsList[0].IsError);
        Assert.IsType<CannotDeleteAdminsError>(resultsList[0].AsError);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_AdminDeletingAdmin_ReturnsValidResult(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        User user,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Admin)] OrganizationUser organizationUser)
    {
        organizationUser.UserId = user.Id;

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = organizationUser.Id,
            OrganizationUser = organizationUser,
            User = user,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        SetupMocks(sutProvider, organizationId, user.Id, OrganizationUserType.Admin);

        var results = await sutProvider.Sut.ValidateAsync([request]);

        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.True(resultsList[0].IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithMixedValidAndInvalidRequests_ReturnsCorrespondingResults(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        User validUser,
        User invalidUser,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser] OrganizationUser validOrgUser,
        [OrganizationUser(OrganizationUserStatusType.Invited)] OrganizationUser invalidOrgUser)
    {
        validOrgUser.UserId = validUser.Id;

        invalidOrgUser.UserId = invalidUser.Id;

        var validRequest = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = validOrgUser.Id,
            OrganizationUser = validOrgUser,
            User = validUser,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        var invalidRequest = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = invalidOrgUser.Id,
            OrganizationUser = invalidOrgUser,
            User = invalidUser,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        SetupMocks(sutProvider, organizationId, validUser.Id);

        var results = await sutProvider.Sut.ValidateAsync([validRequest, invalidRequest]);

        var resultsList = results.ToList();
        Assert.Equal(2, resultsList.Count);

        var validResult = resultsList.First(r => r.Request == validRequest);
        var invalidResult = resultsList.First(r => r.Request == invalidRequest);

        Assert.True(validResult.IsValid);
        Assert.True(invalidResult.IsError);
        Assert.IsType<InvalidUserStatusError>(invalidResult.AsError);
    }

    private static void SetupMocks(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        Guid organizationId,
        Guid userId,
        OrganizationUserType currentUserType = OrganizationUserType.Owner)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationOwner(organizationId)
            .Returns(currentUserType == OrganizationUserType.Owner);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationAdmin(organizationId)
            .Returns(currentUserType is OrganizationUserType.Owner or OrganizationUserType.Admin);

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationCustom(organizationId)
            .Returns(currentUserType is OrganizationUserType.Custom);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByOnlyOwnerAsync(userId)
            .Returns(0);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetCountByOnlyOwnerAsync(userId)
            .Returns(0);
    }
}
