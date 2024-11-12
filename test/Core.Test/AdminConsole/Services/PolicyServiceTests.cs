using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services.Implementations;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using AdminConsoleFixtures = Bit.Core.Test.AdminConsole.AutoFixture;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.AdminConsole.Services;

[SutProviderCustomize]
public class PolicyServiceTests
{
    [Theory, BitAutoData]
    public async Task SaveAsync_OrganizationDoesNotExist_ThrowsBadRequest(
        [AdminConsoleFixtures.Policy(PolicyType.DisableSend)] Policy policy, SutProvider<PolicyService> sutProvider)
    {
        SetupOrg(sutProvider, policy.OrganizationId, null);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Guid.NewGuid()));

        Assert.Contains("Organization not found", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogPolicyEventAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_OrganizationCannotUsePolicies_ThrowsBadRequest(
        [AdminConsoleFixtures.Policy(PolicyType.DisableSend)] Policy policy, SutProvider<PolicyService> sutProvider)
    {
        var orgId = Guid.NewGuid();

        SetupOrg(sutProvider, policy.OrganizationId, new Organization
        {
            UsePolicies = false,
        });

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Guid.NewGuid()));

        Assert.Contains("cannot use policies", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogPolicyEventAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_SingleOrg_RequireSsoEnabled_ThrowsBadRequest(
        [AdminConsoleFixtures.Policy(PolicyType.SingleOrg)] Policy policy, SutProvider<PolicyService> sutProvider)
    {
        policy.Enabled = false;

        SetupOrg(sutProvider, policy.OrganizationId, new Organization
        {
            Id = policy.OrganizationId,
            UsePolicies = true,
        });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policy.OrganizationId, PolicyType.RequireSso)
            .Returns(Task.FromResult(new Policy { Enabled = true }));

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Guid.NewGuid()));

        Assert.Contains("Single Sign-On Authentication policy is enabled.", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogPolicyEventAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_SingleOrg_VaultTimeoutEnabled_ThrowsBadRequest([AdminConsoleFixtures.Policy(PolicyType.SingleOrg)] Policy policy, SutProvider<PolicyService> sutProvider)
    {
        policy.Enabled = false;

        SetupOrg(sutProvider, policy.OrganizationId, new Organization
        {
            Id = policy.OrganizationId,
            UsePolicies = true,
        });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policy.OrganizationId, PolicyType.MaximumVaultTimeout)
            .Returns(new Policy { Enabled = true });

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Guid.NewGuid()));

        Assert.Contains("Maximum Vault Timeout policy is enabled.", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
    }

    [Theory]
    [BitAutoData(PolicyType.SingleOrg)]
    [BitAutoData(PolicyType.RequireSso)]
    public async Task SaveAsync_PolicyRequiredByKeyConnector_DisablePolicy_ThrowsBadRequest(
        PolicyType policyType,
        Policy policy,
        SutProvider<PolicyService> sutProvider)
    {
        policy.Enabled = false;
        policy.Type = policyType;

        SetupOrg(sutProvider, policy.OrganizationId, new Organization
        {
            Id = policy.OrganizationId,
            UsePolicies = true,
        });

        var ssoConfig = new SsoConfig { Enabled = true };
        var data = new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.KeyConnector };
        ssoConfig.SetData(data);

        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(policy.OrganizationId)
            .Returns(ssoConfig);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Guid.NewGuid()));

        Assert.Contains("Key Connector is enabled.", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_RequireSsoPolicy_NotEnabled_ThrowsBadRequestAsync(
        [AdminConsoleFixtures.Policy(PolicyType.RequireSso)] Policy policy, SutProvider<PolicyService> sutProvider)
    {
        policy.Enabled = true;

        SetupOrg(sutProvider, policy.OrganizationId, new Organization
        {
            Id = policy.OrganizationId,
            UsePolicies = true,
        });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policy.OrganizationId, PolicyType.SingleOrg)
            .Returns(Task.FromResult(new Policy { Enabled = false }));

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Guid.NewGuid()));

        Assert.Contains("Single Organization policy not enabled.", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogPolicyEventAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_NewPolicy_Created(
        [AdminConsoleFixtures.Policy(PolicyType.ResetPassword)] Policy policy, SutProvider<PolicyService> sutProvider)
    {
        policy.Id = default;
        policy.Data = null;

        SetupOrg(sutProvider, policy.OrganizationId, new Organization
        {
            Id = policy.OrganizationId,
            UsePolicies = true,
        });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policy.OrganizationId, PolicyType.SingleOrg)
            .Returns(Task.FromResult(new Policy { Enabled = true }));

        var utcNow = DateTime.UtcNow;

        await sutProvider.Sut.SaveAsync(policy, Guid.NewGuid());

        await sutProvider.GetDependency<IEventService>().Received()
            .LogPolicyEventAsync(policy, EventType.Policy_Updated);

        await sutProvider.GetDependency<IPolicyRepository>().Received()
            .UpsertAsync(policy);

        Assert.True(policy.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(policy.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_VaultTimeoutPolicy_NotEnabled_ThrowsBadRequestAsync(
        [AdminConsoleFixtures.Policy(PolicyType.MaximumVaultTimeout)] Policy policy, SutProvider<PolicyService> sutProvider)
    {
        policy.Enabled = true;

        SetupOrg(sutProvider, policy.OrganizationId, new Organization
        {
            Id = policy.OrganizationId,
            UsePolicies = true,
        });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policy.OrganizationId, PolicyType.SingleOrg)
            .Returns(Task.FromResult(new Policy { Enabled = false }));

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Guid.NewGuid()));

        Assert.Contains("Single Organization policy not enabled.", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogPolicyEventAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_ExistingPolicy_UpdateTwoFactor(
        Organization organization,
        [AdminConsoleFixtures.Policy(PolicyType.TwoFactorAuthentication)] Policy policy,
        SutProvider<PolicyService> sutProvider)
    {
        // If the policy that this is updating isn't enabled then do some work now that the current one is enabled

        organization.UsePolicies = true;
        policy.OrganizationId = organization.Id;

        SetupOrg(sutProvider, organization.Id, organization);

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByIdAsync(policy.Id)
            .Returns(new Policy
            {
                Id = policy.Id,
                Type = PolicyType.TwoFactorAuthentication,
                Enabled = false
            });

        var orgUserDetailUserInvited = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Invited,
            Type = OrganizationUserType.User,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "user1@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = false
        };
        var orgUserDetailUserAcceptedWith2FA = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "user2@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = true
        };
        var orgUserDetailUserAcceptedWithout2FA = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "user3@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = true
        };
        var orgUserDetailAdmin = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Admin,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "admin@test.com",
            Name = "ADMIN",
            UserId = Guid.NewGuid(),
            HasMasterPassword = false
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policy.OrganizationId)
            .Returns(new List<OrganizationUserUserDetails>
            {
                orgUserDetailUserInvited,
                orgUserDetailUserAcceptedWith2FA,
                orgUserDetailUserAcceptedWithout2FA,
                orgUserDetailAdmin
            });

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<OrganizationUserUserDetails>>())
            .Returns(new List<(OrganizationUserUserDetails user, bool hasTwoFactor)>()
            {
                (orgUserDetailUserInvited, false),
                (orgUserDetailUserAcceptedWith2FA, true),
                (orgUserDetailUserAcceptedWithout2FA, false),
                (orgUserDetailAdmin, false),
            });

        var removeOrganizationUserCommand = sutProvider.GetDependency<IRemoveOrganizationUserCommand>();

        var utcNow = DateTime.UtcNow;

        var savingUserId = Guid.NewGuid();

        await sutProvider.Sut.SaveAsync(policy, savingUserId);

        await removeOrganizationUserCommand.Received()
            .RemoveUserAsync(policy.OrganizationId, orgUserDetailUserAcceptedWithout2FA.Id, savingUserId);
        await sutProvider.GetDependency<IMailService>().Received()
            .SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(organization.DisplayName(), orgUserDetailUserAcceptedWithout2FA.Email);

        await removeOrganizationUserCommand.DidNotReceive()
            .RemoveUserAsync(policy.OrganizationId, orgUserDetailUserInvited.Id, savingUserId);
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(organization.DisplayName(), orgUserDetailUserInvited.Email);
        await removeOrganizationUserCommand.DidNotReceive()
            .RemoveUserAsync(policy.OrganizationId, orgUserDetailUserAcceptedWith2FA.Id, savingUserId);
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(organization.DisplayName(), orgUserDetailUserAcceptedWith2FA.Email);
        await removeOrganizationUserCommand.DidNotReceive()
            .RemoveUserAsync(policy.OrganizationId, orgUserDetailAdmin.Id, savingUserId);
        await sutProvider.GetDependency<IMailService>().DidNotReceive()
            .SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(organization.DisplayName(), orgUserDetailAdmin.Email);

        await sutProvider.GetDependency<IEventService>().Received()
            .LogPolicyEventAsync(policy, EventType.Policy_Updated);

        await sutProvider.GetDependency<IPolicyRepository>().Received()
            .UpsertAsync(policy);

        Assert.True(policy.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(policy.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_EnableTwoFactor_WithoutMasterPasswordOr2FA_ThrowsBadRequest(
        Organization organization,
        [AdminConsoleFixtures.Policy(PolicyType.TwoFactorAuthentication)] Policy policy,
        SutProvider<PolicyService> sutProvider)
    {
        organization.UsePolicies = true;
        policy.OrganizationId = organization.Id;

        SetupOrg(sutProvider, organization.Id, organization);

        var orgUserDetailUserWith2FAAndMP = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "user1@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = true
        };
        var orgUserDetailUserWith2FANoMP = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "user2@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = false
        };
        var orgUserDetailUserWithout2FA = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "user3@test.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = false
        };
        var orgUserDetailAdmin = new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Admin,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "admin@test.com",
            Name = "ADMIN",
            UserId = Guid.NewGuid(),
            HasMasterPassword = false
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policy.OrganizationId)
            .Returns(new List<OrganizationUserUserDetails>
            {
                orgUserDetailUserWith2FAAndMP,
                orgUserDetailUserWith2FANoMP,
                orgUserDetailUserWithout2FA,
                orgUserDetailAdmin
            });

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Contains(orgUserDetailUserWith2FANoMP.UserId.Value)
                && ids.Contains(orgUserDetailUserWithout2FA.UserId.Value)
                && ids.Contains(orgUserDetailAdmin.UserId.Value)))
            .Returns(new List<(Guid userId, bool hasTwoFactor)>()
            {
                (orgUserDetailUserWith2FANoMP.UserId.Value, true),
                (orgUserDetailUserWithout2FA.UserId.Value, false),
                (orgUserDetailAdmin.UserId.Value, false),
            });

        var removeOrganizationUserCommand = sutProvider.GetDependency<IRemoveOrganizationUserCommand>();

        var savingUserId = Guid.NewGuid();

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy, savingUserId));

        Assert.Contains("Policy could not be enabled. Non-compliant members will lose access to their accounts. Identify members without two-step login from the policies column in the members page.", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

        await removeOrganizationUserCommand.DidNotReceiveWithAnyArgs()
            .RemoveUserAsync(organizationId: default, organizationUserId: default, deletingUserId: default);

        await sutProvider.GetDependency<IMailService>().DidNotReceiveWithAnyArgs()
            .SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(default, default);

        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogPolicyEventAsync(default, default);

        await sutProvider.GetDependency<IPolicyRepository>().DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_ExistingPolicy_UpdateSingleOrg(
        [AdminConsoleFixtures.Policy(PolicyType.TwoFactorAuthentication)] Policy policy, SutProvider<PolicyService> sutProvider)
    {
        // If the policy that this is updating isn't enabled then do some work now that the current one is enabled

        var org = new Organization
        {
            Id = policy.OrganizationId,
            UsePolicies = true,
            Name = "TEST",
        };

        SetupOrg(sutProvider, policy.OrganizationId, org);

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByIdAsync(policy.Id)
            .Returns(new Policy
            {
                Id = policy.Id,
                Type = PolicyType.SingleOrg,
                Enabled = false,
            });

        var orgUserDetail = new Core.Models.Data.Organizations.OrganizationUsers.OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            Status = OrganizationUserStatusType.Accepted,
            Type = OrganizationUserType.User,
            // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
            Email = "test@bitwarden.com",
            Name = "TEST",
            UserId = Guid.NewGuid(),
            HasMasterPassword = true
        };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(policy.OrganizationId)
            .Returns(new List<Core.Models.Data.Organizations.OrganizationUsers.OrganizationUserUserDetails>
            {
                orgUserDetail,
            });

        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(orgUserDetail.UserId.Value)))
            .Returns(new List<(Guid userId, bool hasTwoFactor)>()
            {
                (orgUserDetail.UserId.Value, false),
            });

        var utcNow = DateTime.UtcNow;

        var savingUserId = Guid.NewGuid();

        await sutProvider.Sut.SaveAsync(policy, savingUserId);

        await sutProvider.GetDependency<IEventService>().Received()
            .LogPolicyEventAsync(policy, EventType.Policy_Updated);

        await sutProvider.GetDependency<IPolicyRepository>().Received()
            .UpsertAsync(policy);

        Assert.True(policy.CreationDate - utcNow < TimeSpan.FromSeconds(1));
        Assert.True(policy.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
    }

    [Theory]
    [BitAutoData(true, false)]
    [BitAutoData(false, true)]
    [BitAutoData(false, false)]
    public async Task SaveAsync_ResetPasswordPolicyRequiredByTrustedDeviceEncryption_DisablePolicyOrDisableAutomaticEnrollment_ThrowsBadRequest(
        bool policyEnabled,
        bool autoEnrollEnabled,
        [AdminConsoleFixtures.Policy(PolicyType.ResetPassword)] Policy policy,
        SutProvider<PolicyService> sutProvider)
    {
        policy.Enabled = policyEnabled;
        policy.SetDataModel(new ResetPasswordDataModel
        {
            AutoEnrollEnabled = autoEnrollEnabled
        });

        SetupOrg(sutProvider, policy.OrganizationId, new Organization
        {
            Id = policy.OrganizationId,
            UsePolicies = true,
        });

        var ssoConfig = new SsoConfig { Enabled = true };
        ssoConfig.SetData(new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption });

        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(policy.OrganizationId)
            .Returns(ssoConfig);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Guid.NewGuid()));

        Assert.Contains("Trusted device encryption is on and requires this policy.", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogPolicyEventAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_RequireSsoPolicyRequiredByTrustedDeviceEncryption_DisablePolicy_ThrowsBadRequest(
        [AdminConsoleFixtures.Policy(PolicyType.RequireSso)] Policy policy,
        SutProvider<PolicyService> sutProvider)
    {
        policy.Enabled = false;

        SetupOrg(sutProvider, policy.OrganizationId, new Organization
        {
            Id = policy.OrganizationId,
            UsePolicies = true,
        });

        var ssoConfig = new SsoConfig { Enabled = true };
        ssoConfig.SetData(new SsoConfigurationData { MemberDecryptionType = MemberDecryptionType.TrustedDeviceEncryption });

        sutProvider.GetDependency<ISsoConfigRepository>()
            .GetByOrganizationIdAsync(policy.OrganizationId)
            .Returns(ssoConfig);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Guid.NewGuid()));

        Assert.Contains("Trusted device encryption is on and requires this policy.", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogPolicyEventAsync(default, default, default);
    }

    [Theory, BitAutoData]
    public async Task SaveAsync_PolicyRequiredForAccountRecovery_NotEnabled_ThrowsBadRequestAsync(
        [AdminConsoleFixtures.Policy(PolicyType.ResetPassword)] Policy policy, SutProvider<PolicyService> sutProvider)
    {
        policy.Enabled = true;
        policy.SetDataModel(new ResetPasswordDataModel());

        SetupOrg(sutProvider, policy.OrganizationId, new Organization
        {
            Id = policy.OrganizationId,
            UsePolicies = true,
        });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policy.OrganizationId, PolicyType.SingleOrg)
            .Returns(Task.FromResult(new Policy { Enabled = false }));

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Guid.NewGuid()));

        Assert.Contains("Single Organization policy not enabled.", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);

        await sutProvider.GetDependency<IEventService>()
            .DidNotReceiveWithAnyArgs()
            .LogPolicyEventAsync(default, default, default);
    }


    [Theory, BitAutoData]
    public async Task SaveAsync_SingleOrg_AccountRecoveryEnabled_ThrowsBadRequest(
        [AdminConsoleFixtures.Policy(PolicyType.SingleOrg)] Policy policy, SutProvider<PolicyService> sutProvider)
    {
        policy.Enabled = false;

        SetupOrg(sutProvider, policy.OrganizationId, new Organization
        {
            Id = policy.OrganizationId,
            UsePolicies = true,
        });

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(policy.OrganizationId, PolicyType.ResetPassword)
            .Returns(new Policy { Enabled = true });

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy,
                Guid.NewGuid()));

        Assert.Contains("Account recovery policy is enabled.", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

        await sutProvider.GetDependency<IPolicyRepository>()
            .DidNotReceiveWithAnyArgs()
            .UpsertAsync(default);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithRequireSsoTypeFilter_WithDefaultOrganizationUserStatusFilter_ReturnsNoPolicies(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.RequireSso);

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithRequireSsoTypeFilter_WithDefaultOrganizationUserStatusFilter_ReturnsOnePolicy(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        sutProvider.GetDependency<GlobalSettings>().Sso.EnforceSsoPolicyForAllUsers.Returns(true);

        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.RequireSso);

        Assert.Single(result);
        Assert.True(result.All(details => details.PolicyEnabled));
        Assert.True(result.All(details => details.PolicyType == PolicyType.RequireSso));
        Assert.True(result.All(details => details.OrganizationUserType == OrganizationUserType.Owner));
        Assert.True(result.All(details => details.OrganizationUserStatus == OrganizationUserStatusType.Confirmed));
        Assert.True(result.All(details => !details.IsProvider));
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithDisableTypeFilter_WithDefaultOrganizationUserStatusFilter_ReturnsNoPolicies(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.DisableSend);

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task GetPoliciesApplicableToUserAsync_WithDisableSendTypeFilter_WithInvitedUserStatusFilter_ReturnsOnePolicy(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        var result = await sutProvider.Sut
            .GetPoliciesApplicableToUserAsync(userId, PolicyType.DisableSend, OrganizationUserStatusType.Invited);

        Assert.Single(result);
        Assert.True(result.All(details => details.PolicyEnabled));
        Assert.True(result.All(details => details.PolicyType == PolicyType.DisableSend));
        Assert.True(result.All(details => details.OrganizationUserType == OrganizationUserType.User));
        Assert.True(result.All(details => details.OrganizationUserStatus == OrganizationUserStatusType.Invited));
        Assert.True(result.All(details => !details.IsProvider));
    }

    [Theory, BitAutoData]
    public async Task AnyPoliciesApplicableToUserAsync_WithRequireSsoTypeFilter_WithDefaultOrganizationUserStatusFilter_ReturnsFalse(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        var result = await sutProvider.Sut
            .AnyPoliciesApplicableToUserAsync(userId, PolicyType.RequireSso);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task AnyPoliciesApplicableToUserAsync_WithRequireSsoTypeFilter_WithDefaultOrganizationUserStatusFilter_ReturnsTrue(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        sutProvider.GetDependency<GlobalSettings>().Sso.EnforceSsoPolicyForAllUsers.Returns(true);

        var result = await sutProvider.Sut
            .AnyPoliciesApplicableToUserAsync(userId, PolicyType.RequireSso);

        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task AnyPoliciesApplicableToUserAsync_WithDisableTypeFilter_WithDefaultOrganizationUserStatusFilter_ReturnsFalse(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        var result = await sutProvider.Sut
            .AnyPoliciesApplicableToUserAsync(userId, PolicyType.DisableSend);

        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task AnyPoliciesApplicableToUserAsync_WithDisableSendTypeFilter_WithInvitedUserStatusFilter_ReturnsTrue(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        SetupUserPolicies(userId, sutProvider);

        var result = await sutProvider.Sut
            .AnyPoliciesApplicableToUserAsync(userId, PolicyType.DisableSend, OrganizationUserStatusType.Invited);

        Assert.True(result);
    }

    private static void SetupOrg(SutProvider<PolicyService> sutProvider, Guid organizationId, Organization organization)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(Task.FromResult(organization));
    }

    private static void SetupUserPolicies(Guid userId, SutProvider<PolicyService> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.RequireSso)
            .Returns(new List<OrganizationUserPolicyDetails>
            {
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.RequireSso, PolicyEnabled = false, OrganizationUserType = OrganizationUserType.Owner, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false},
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.RequireSso, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.Owner, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = false },
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.RequireSso, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.Owner, OrganizationUserStatus = OrganizationUserStatusType.Confirmed, IsProvider = true }
            });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.DisableSend)
            .Returns(new List<OrganizationUserPolicyDetails>
            {
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.DisableSend, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Invited, IsProvider = false },
                new() { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.DisableSend, PolicyEnabled = true, OrganizationUserType = OrganizationUserType.User, OrganizationUserStatus = OrganizationUserStatusType.Invited, IsProvider = true }
            });
    }


    [Theory, BitAutoData]
    public async Task SaveAsync_GivenOrganizationUsingPoliciesAndHasVerifiedDomains_WhenSingleOrgPolicyIsDisabled_ThenAnErrorShouldBeThrownOrganizationHasVerifiedDomains(
        [AdminConsoleFixtures.Policy(PolicyType.SingleOrg)] Policy policy, Organization org, SutProvider<PolicyService> sutProvider)
    {
        org.Id = policy.OrganizationId;
        org.UsePolicies = true;

        policy.Enabled = false;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.AccountDeprovisioning)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(policy.OrganizationId)
            .Returns(org);

        sutProvider.GetDependency<IOrganizationHasVerifiedDomainsQuery>()
            .HasVerifiedDomainsAsync(org.Id)
            .Returns(true);

        var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SaveAsync(policy, null));

        Assert.Equal("The Single organization policy is required for organizations that have enabled domain verification.", badRequestException.Message);
    }
}
