using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
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
        Assert.IsType<OnlyOwnersCanManageOwners>(resultsList[0].AsError);
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
        Assert.IsType<CustomUsersCannotManageAdminsOrOwners>(resultsList[0].AsError);
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

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithMixedRoleBatch_ReturnsPerTargetManageResults(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        User ownerTargetUser,
        User adminTargetUser,
        User userTargetUser,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser ownerOrgUser,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Admin)] OrganizationUser adminOrgUser,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser userOrgUser)
    {
        // A batch mixing an Owner, an Admin, and a plain User target, deleted by an Admin: the Owner target should
        // be denied while the Admin and User targets succeed, matching the single-target "can manage" hierarchy.
        ownerOrgUser.UserId = ownerTargetUser.Id;
        adminOrgUser.UserId = adminTargetUser.Id;
        userOrgUser.UserId = userTargetUser.Id;

        var ownerRequest = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = ownerOrgUser.Id,
            OrganizationUser = ownerOrgUser,
            User = ownerTargetUser,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        var adminRequest = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = adminOrgUser.Id,
            OrganizationUser = adminOrgUser,
            User = adminTargetUser,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        var userRequest = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = userOrgUser.Id,
            OrganizationUser = userOrgUser,
            User = userTargetUser,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        SetupMocks(sutProvider, organizationId, deletingUserId, OrganizationUserType.Admin);

        var results = await sutProvider.Sut.ValidateAsync([ownerRequest, adminRequest, userRequest]);

        var resultsList = results.ToList();
        Assert.Equal(3, resultsList.Count);

        var ownerResult = resultsList.Single(r => r.Request == ownerRequest);
        var adminResult = resultsList.Single(r => r.Request == adminRequest);
        var userResult = resultsList.Single(r => r.Request == userRequest);

        Assert.True(ownerResult.IsError);
        Assert.IsType<OnlyOwnersCanManageOwners>(ownerResult.AsError);
        Assert.True(adminResult.IsValid);
        Assert.True(userResult.IsValid);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WithDuplicateOrganizationUserIds_DoesNotThrowAndReturnsResultPerRequest(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        User user,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser] OrganizationUser organizationUser)
    {
        // A caller-supplied bulk payload isn't de-duplicated upstream, so the same OrganizationUserId can appear
        // more than once; this must not throw when grouping targets for the batched "can manage" check.
        organizationUser.UserId = user.Id;
        organizationUser.OrganizationId = organizationId;

        var request1 = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = organizationUser.Id,
            OrganizationUser = organizationUser,
            User = user,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        var request2 = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = organizationUser.Id,
            OrganizationUser = organizationUser,
            User = user,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        SetupMocks(sutProvider, organizationId, user.Id);

        var results = await sutProvider.Sut.ValidateAsync([request1, request2]);

        var resultsList = results.ToList();
        Assert.Equal(2, resultsList.Count);
        Assert.All(resultsList, result => Assert.True(result.IsValid));
    }

    private static void SetupMocks(
        SutProvider<DeleteClaimedOrganizationUserAccountValidator> sutProvider,
        Guid organizationId,
        Guid userId,
        OrganizationUserType currentUserType = OrganizationUserType.Owner)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .GetOrganization(organizationId)
            .Returns(new CurrentContextOrganization
            {
                Id = organizationId,
                Type = currentUserType,
                Permissions = new Permissions { ManageUsers = true }
            });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByOnlyOwnerAsync(userId)
            .Returns(0);

        var providerUserRepository = sutProvider.GetDependency<IProviderUserRepository>();
        providerUserRepository.GetCountByOnlyOwnerAsync(userId).Returns(0);
        providerUserRepository
            .GetManyOrganizationDetailsByUserAsync(Arg.Any<Guid>(), ProviderUserStatusType.Confirmed)
            .Returns([]);

        // Use a real validation service (backed by the same, already-mocked repositories) so these tests exercise
        // the real "can manage" hierarchy instead of re-implementing it as a mock.
        var validationService = new OrganizationUserValidationService(
            providerUserRepository, sutProvider.GetDependency<IOrganizationUserRepository>());
        sutProvider.SetDependency<IOrganizationUserValidationService>(validationService, "organizationUserValidationService");
        sutProvider.Create();
    }
}
