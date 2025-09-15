using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.AdminConsole.Utilities.Commands;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

[SutProviderCustomize]
public class TwoFactorAuthenticationPolicyHandlerTests
{
    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_GivenNonCompliantUsersWithoutMasterPassword_Throws(
        Organization organization,
        [PolicyUpdate(PolicyType.TwoFactorAuthentication)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.TwoFactorAuthentication, false)] Policy policy,
        SutProvider<TwoFactorAuthenticationPolicyHandler> sutProvider)
    {
        policy.OrganizationId = organization.Id = policyUpdate.OrganizationId;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var orgUserDetailUserWithout2Fa = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            Email = "user3@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = false
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([orgUserDetailUserWithout2Fa]);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<OrganizationUserUserDetails>>())
            .Returns(new List<(OrganizationUserUserDetails user, bool hasTwoFactor)>()
            {
                (orgUserDetailUserWithout2Fa, false),
            });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.ExecutePreUpsertSideEffectAsync(policyUpdate, policy));

        Assert.Equal(TwoFactorAuthenticationPolicyHandler.NonCompliantMembersWillLoseAccessMessage, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_RevokesOnlyNonCompliantUsers(
        Organization organization,
        [PolicyUpdate(PolicyType.TwoFactorAuthentication)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.TwoFactorAuthentication, false)] Policy policy,
        SutProvider<TwoFactorAuthenticationPolicyHandler> sutProvider)
    {
        // Arrange
        policy.OrganizationId = policyUpdate.OrganizationId;
        organization.Id = policyUpdate.OrganizationId;

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var nonCompliantUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            Email = "user3@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = true
        };

        var compliantUser = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            Email = "user4@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = true
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([nonCompliantUser, compliantUser]);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<OrganizationUserUserDetails>>())
            .Returns(new List<(OrganizationUserUserDetails user, bool hasTwoFactor)>()
            {
                (nonCompliantUser, false),
                (compliantUser, true)
            });

        sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .RevokeNonCompliantOrganizationUsersAsync(Arg.Any<RevokeOrganizationUsersRequest>())
            .Returns(new CommandResult());

        // Act
        await sutProvider.Sut.ExecutePreUpsertSideEffectAsync(policyUpdate, policy);

        // Assert
        await sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .Received(1)
            .RevokeNonCompliantOrganizationUsersAsync(Arg.Any<RevokeOrganizationUsersRequest>());

        await sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .Received(1)
            .RevokeNonCompliantOrganizationUsersAsync(Arg.Is<RevokeOrganizationUsersRequest>(req =>
                    req.OrganizationId == policyUpdate.OrganizationId &&
                    req.OrganizationUsers.SequenceEqual(new[] { nonCompliantUser })
            ));

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationUserRevokedForTwoFactorPolicyEmailAsync(organization.DisplayName(),
                nonCompliantUser.Email);

        // Did not send out an email for compliantUser
        await sutProvider.GetDependency<IMailService>()
            .Received(0)
            .SendOrganizationUserRevokedForTwoFactorPolicyEmailAsync(organization.DisplayName(),
                compliantUser.Email);
    }
}
