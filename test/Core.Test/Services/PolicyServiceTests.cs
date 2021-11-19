using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using PolicyFixtures = Bit.Core.Test.AutoFixture.PolicyFixtures;
using NSubstitute;
using Xunit;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.AutoFixture;

namespace Bit.Core.Test.Services
{
    public class PolicyServiceTests
    {
        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_OrganizationDoesNotExist_ThrowsBadRequest([PolicyFixtures.Policy(PolicyType.DisableSend)] Core.Models.Table.Policy policy, SutProvider<PolicyService> sutProvider)
        {
            SetupOrg(sutProvider, policy.OrganizationId, null);

            var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveAsync(policy,
                    Substitute.For<IUserService>(),
                    Substitute.For<IOrganizationService>(),
                    Guid.NewGuid()));

            Assert.Contains("Organization not found", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

            await sutProvider.GetDependency<IPolicyRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);

            await sutProvider.GetDependency<IEventService>()
                .DidNotReceiveWithAnyArgs()
                .LogPolicyEventAsync(default, default, default);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_OrganizationCannotUsePolicies_ThrowsBadRequest([PolicyFixtures.Policy(PolicyType.DisableSend)] Core.Models.Table.Policy policy, SutProvider<PolicyService> sutProvider)
        {
            var orgId = Guid.NewGuid();

            SetupOrg(sutProvider, policy.OrganizationId, new Organization 
            {
                UsePolicies = false,
            });

            var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveAsync(policy,
                    Substitute.For<IUserService>(),
                    Substitute.For<IOrganizationService>(),
                    Guid.NewGuid()));

            Assert.Contains("cannot use policies", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

            await sutProvider.GetDependency<IPolicyRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);

            await sutProvider.GetDependency<IEventService>()
                .DidNotReceiveWithAnyArgs()
                .LogPolicyEventAsync(default, default, default);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_SingleOrg_RequireSsoEnabled_ThrowsBadRequest([PolicyFixtures.Policy(PolicyType.SingleOrg)] Core.Models.Table.Policy policy, SutProvider<PolicyService> sutProvider)
        {
            policy.Enabled = false;

            SetupOrg(sutProvider, policy.OrganizationId, new Organization
            {
                Id = policy.OrganizationId,
                UsePolicies = true,
            });

            sutProvider.GetDependency<IPolicyRepository>()
                .GetByOrganizationIdTypeAsync(policy.OrganizationId, PolicyType.RequireSso)
                .Returns(Task.FromResult(new Core.Models.Table.Policy { Enabled = true }));

            var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveAsync(policy,
                    Substitute.For<IUserService>(),
                    Substitute.For<IOrganizationService>(),
                    Guid.NewGuid()));

            Assert.Contains("Single Sign-On Authentication policy is enabled.", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

            await sutProvider.GetDependency<IPolicyRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);

            await sutProvider.GetDependency<IEventService>()
                .DidNotReceiveWithAnyArgs()
                .LogPolicyEventAsync(default, default, default);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_SingleOrg_VaultTimeoutEnabled_ThrowsBadRequest([PolicyFixtures.Policy(Enums.PolicyType.SingleOrg)] Core.Models.Table.Policy policy, SutProvider<PolicyService> sutProvider)
        {
            policy.Enabled = false;

            SetupOrg(sutProvider, policy.OrganizationId, new Organization
            {
                Id = policy.OrganizationId,
                UsePolicies = true,
            });

            sutProvider.GetDependency<IPolicyRepository>()
                .GetByOrganizationIdTypeAsync(policy.OrganizationId, Enums.PolicyType.MaximumVaultTimeout)
                .Returns(new Policy { Enabled = true });

            var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveAsync(policy,
                    Substitute.For<IUserService>(),
                    Substitute.For<IOrganizationService>(),
                    Guid.NewGuid()));

            Assert.Contains("Maximum Vault Timeout policy is enabled.", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

            await sutProvider.GetDependency<IPolicyRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
        }

        [Theory]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, Enums.PolicyType.SingleOrg)]
        [InlineCustomAutoData(new[] { typeof(SutProviderCustomization) }, Enums.PolicyType.RequireSso)]
        public async Task SaveAsync_PolicyRequiredByKeyConnector_DisablePolicy_ThrowsBadRequest(
            Enums.PolicyType policyType,
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
            var data = new SsoConfigurationData { KeyConnectorEnabled = true };
            ssoConfig.SetData(data);

            sutProvider.GetDependency<ISsoConfigRepository>()
                .GetByOrganizationIdAsync(policy.OrganizationId)
                .Returns(ssoConfig);

            var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveAsync(policy,
                    Substitute.For<IUserService>(),
                    Substitute.For<IOrganizationService>(),
                    Guid.NewGuid()));

            Assert.Contains("Key Connector is enabled.", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

            await sutProvider.GetDependency<IPolicyRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_RequireSsoPolicy_NotEnabled_ThrowsBadRequestAsync([PolicyFixtures.Policy(Enums.PolicyType.RequireSso)] Core.Models.Table.Policy policy, SutProvider<PolicyService> sutProvider)
        {
            policy.Enabled = true;

            SetupOrg(sutProvider, policy.OrganizationId, new Organization
            {
                Id = policy.OrganizationId,
                UsePolicies = true,
            });

            sutProvider.GetDependency<IPolicyRepository>()
                .GetByOrganizationIdTypeAsync(policy.OrganizationId, PolicyType.SingleOrg)
                .Returns(Task.FromResult(new Core.Models.Table.Policy { Enabled = false }));

            var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveAsync(policy,
                    Substitute.For<IUserService>(),
                    Substitute.For<IOrganizationService>(),
                    Guid.NewGuid()));

            Assert.Contains("Single Organization policy not enabled.", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

            await sutProvider.GetDependency<IPolicyRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);

            await sutProvider.GetDependency<IEventService>()
                .DidNotReceiveWithAnyArgs()
                .LogPolicyEventAsync(default, default, default);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_NewPolicy_Created([PolicyFixtures.Policy(PolicyType.MasterPassword)] Core.Models.Table.Policy policy, SutProvider<PolicyService> sutProvider)
        {
            policy.Id = default;

            SetupOrg(sutProvider, policy.OrganizationId, new Organization
            {
                Id = policy.OrganizationId,
                UsePolicies = true,
            });

            var utcNow = DateTime.UtcNow;

            await sutProvider.Sut.SaveAsync(policy, Substitute.For<IUserService>(), Substitute.For<IOrganizationService>(), Guid.NewGuid());

            await sutProvider.GetDependency<IEventService>().Received()
                .LogPolicyEventAsync(policy, EventType.Policy_Updated);

            await sutProvider.GetDependency<IPolicyRepository>().Received()
                .UpsertAsync(policy);

            Assert.True(policy.CreationDate - utcNow < TimeSpan.FromSeconds(1));
            Assert.True(policy.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_VaultTimeoutPolicy_NotEnabled_ThrowsBadRequestAsync([PolicyFixtures.Policy(Enums.PolicyType.MaximumVaultTimeout)] Core.Models.Table.Policy policy, SutProvider<PolicyService> sutProvider)
        {
            policy.Enabled = true;

            SetupOrg(sutProvider, policy.OrganizationId, new Organization
            {
                Id = policy.OrganizationId,
                UsePolicies = true,
            });

            sutProvider.GetDependency<IPolicyRepository>()
                .GetByOrganizationIdTypeAsync(policy.OrganizationId, Enums.PolicyType.SingleOrg)
                .Returns(Task.FromResult(new Core.Models.Table.Policy { Enabled = false }));

            var badRequestException = await Assert.ThrowsAsync<BadRequestException>(
                () => sutProvider.Sut.SaveAsync(policy,
                    Substitute.For<IUserService>(),
                    Substitute.For<IOrganizationService>(),
                    Guid.NewGuid()));

            Assert.Contains("Single Organization policy not enabled.", badRequestException.Message, StringComparison.OrdinalIgnoreCase);

            await sutProvider.GetDependency<IPolicyRepository>()
                .DidNotReceiveWithAnyArgs()
                .UpsertAsync(default);

            await sutProvider.GetDependency<IEventService>()
                .DidNotReceiveWithAnyArgs()
                .LogPolicyEventAsync(default, default, default);
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_ExistingPolicy_UpdateTwoFactor([PolicyFixtures.Policy(PolicyType.TwoFactorAuthentication)] Core.Models.Table.Policy policy, SutProvider<PolicyService> sutProvider)
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
                .Returns(new Core.Models.Table.Policy
                { 
                    Id = policy.Id,
                    Type = PolicyType.TwoFactorAuthentication,
                    Enabled = false,
                });

            var orgUserDetail = new Core.Models.Data.OrganizationUserUserDetails
            {
                Id = Guid.NewGuid(),
                Status = OrganizationUserStatusType.Accepted,
                Type = OrganizationUserType.User,
                // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
                Email = "test@bitwarden.com",
                Name = "TEST",
                UserId = Guid.NewGuid(),
            };

            sutProvider.GetDependency<IOrganizationUserRepository>()
                .GetManyDetailsByOrganizationAsync(policy.OrganizationId)
                .Returns(new List<Core.Models.Data.OrganizationUserUserDetails>
                {
                    orgUserDetail,
                });

            var userService = Substitute.For<IUserService>();
            var organizationService = Substitute.For<IOrganizationService>();

            userService.TwoFactorIsEnabledAsync(orgUserDetail)
                .Returns(false);

            var utcNow = DateTime.UtcNow;

            var savingUserId = Guid.NewGuid();

            await sutProvider.Sut.SaveAsync(policy, userService, organizationService, savingUserId);

            await organizationService.Received()
                .DeleteUserAsync(policy.OrganizationId, orgUserDetail.Id, savingUserId);

            await sutProvider.GetDependency<IMailService>().Received()
                .SendOrganizationUserRemovedForPolicyTwoStepEmailAsync(org.Name, orgUserDetail.Email);

            await sutProvider.GetDependency<IEventService>().Received()
                .LogPolicyEventAsync(policy, EventType.Policy_Updated);

            await sutProvider.GetDependency<IPolicyRepository>().Received()
                .UpsertAsync(policy);

            Assert.True(policy.CreationDate - utcNow < TimeSpan.FromSeconds(1));
            Assert.True(policy.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        }

        [Theory, CustomAutoData(typeof(SutProviderCustomization))]
        public async Task SaveAsync_ExistingPolicy_UpdateSingleOrg([PolicyFixtures.Policy(PolicyType.TwoFactorAuthentication)] Core.Models.Table.Policy policy, SutProvider<PolicyService> sutProvider)
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
                .Returns(new Core.Models.Table.Policy
                {
                    Id = policy.Id,
                    Type = PolicyType.SingleOrg,
                    Enabled = false,
                });

            var orgUserDetail = new Core.Models.Data.OrganizationUserUserDetails
            {
                Id = Guid.NewGuid(),
                Status = OrganizationUserStatusType.Accepted,
                Type = OrganizationUserType.User,
                // Needs to be different from what is passed in as the savingUserId to Sut.SaveAsync
                Email = "test@bitwarden.com",
                Name = "TEST",
                UserId = Guid.NewGuid(),
            };

            sutProvider.GetDependency<IOrganizationUserRepository>()
                .GetManyDetailsByOrganizationAsync(policy.OrganizationId)
                .Returns(new List<Core.Models.Data.OrganizationUserUserDetails>
                {
                    orgUserDetail,
                });

            var userService = Substitute.For<IUserService>();
            var organizationService = Substitute.For<IOrganizationService>();

            userService.TwoFactorIsEnabledAsync(orgUserDetail)
                .Returns(false);

            var utcNow = DateTime.UtcNow;

            var savingUserId = Guid.NewGuid();

            await sutProvider.Sut.SaveAsync(policy, userService, organizationService, savingUserId);

            await sutProvider.GetDependency<IEventService>().Received()
                .LogPolicyEventAsync(policy, EventType.Policy_Updated);

            await sutProvider.GetDependency<IPolicyRepository>().Received()
                .UpsertAsync(policy);

            Assert.True(policy.CreationDate - utcNow < TimeSpan.FromSeconds(1));
            Assert.True(policy.RevisionDate - utcNow < TimeSpan.FromSeconds(1));
        }

        private static void SetupOrg(SutProvider<PolicyService> sutProvider, Guid organizationId, Organization organization)
        {
            sutProvider.GetDependency<IOrganizationRepository>()
                .GetByIdAsync(organizationId)
                .Returns(Task.FromResult(organization));
        }
    }
}
