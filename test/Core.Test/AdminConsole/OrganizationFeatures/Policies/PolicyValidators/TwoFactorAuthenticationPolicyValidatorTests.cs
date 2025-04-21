using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Commands;
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
public class TwoFactorAuthenticationPolicyValidatorTests
{
    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_GivenNonCompliantUsersWithoutMasterPassword_Throws(
        Organization organization,
        [PolicyUpdate(PolicyType.TwoFactorAuthentication)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.TwoFactorAuthentication, false)] Policy policy,
        SutProvider<TwoFactorAuthenticationPolicyValidator> sutProvider)
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

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, policy));

        Assert.Equal(TwoFactorAuthenticationPolicyValidator.NonCompliantMembersWillLoseAccessMessage, exception.Message);
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_RevokesNonCompliantUsers(
        Organization organization,
        [PolicyUpdate(PolicyType.TwoFactorAuthentication)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.TwoFactorAuthentication, false)] Policy policy,
        SutProvider<TwoFactorAuthenticationPolicyValidator> sutProvider)
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
            HasMasterPassword = true
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policyUpdate.OrganizationId)
            .Returns([orgUserDetailUserWithout2Fa]);

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<OrganizationUserUserDetails>>())
            .Returns(new List<(OrganizationUserUserDetails user, bool hasTwoFactor)>()
            {
                (orgUserDetailUserWithout2Fa, false)
            });

        sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .RevokeNonCompliantOrganizationUsersAsync(Arg.Any<RevokeOrganizationUsersRequest>())
            .Returns(new CommandResult());

        await sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, policy);

        await sutProvider.GetDependency<IRevokeNonCompliantOrganizationUserCommand>()
            .Received(1)
            .RevokeNonCompliantOrganizationUsersAsync(Arg.Any<RevokeOrganizationUsersRequest>());

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationUserRevokedForTwoFactorPolicyEmailAsync(organization.DisplayName(),
                "user3@test.com");
    }
}
