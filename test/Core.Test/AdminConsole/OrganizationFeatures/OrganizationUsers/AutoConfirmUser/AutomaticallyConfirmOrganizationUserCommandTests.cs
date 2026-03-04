using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Models.Data.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

[SutProviderCustomize]
public class AutomaticallyConfirmOrganizationUserCommandTests
{
    [Theory, BitAutoData]
    public async Task AutomaticallyConfirmOrganizationUserAsync_UseMyItemsDisabled_DoesNotCreateCollection(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser,
        string key,
        string collectionName,
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organization.UseMyItems = false;
        orgUser.OrganizationId = organization.Id;

        SetupRepositoryMocks(sutProvider, organization, orgUser);

        // Mock positive validation result
        var validationRequest = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            OrganizationUserId = orgUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = collectionName,
            PerformedBy = null,
            OrganizationUser = orgUser,
            Organization = organization
        };
        sutProvider.GetDependency<IAutomaticallyConfirmOrganizationUsersValidator>()
            .ValidateAsync(Arg.Any<AutomaticallyConfirmOrganizationUserValidationRequest>())
            .Returns(Valid(validationRequest));

        // Mock enabled policy requirement
        var policyDetails = new PolicyDetails
        {
            OrganizationId = organization.Id,
            OrganizationUserId = orgUser.Id,
            IsProvider = false,
            OrganizationUserStatus = orgUser.Status,
            OrganizationUserType = orgUser.Type,
            PolicyType = PolicyType.OrganizationDataOwnership
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(orgUser.UserId!.Value)
            .Returns(new OrganizationDataOwnershipPolicyRequirement(OrganizationDataOwnershipState.Enabled, [policyDetails]));

        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = orgUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = collectionName,
            PerformedBy = null
        };

        // Act
        await sutProvider.Sut.AutomaticallyConfirmOrganizationUserAsync(request);

        // Assert - Collection repository should NOT be called
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .CreateDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task AutomaticallyConfirmOrganizationUserAsync_UseMyItemsEnabled_CreatesCollection(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser,
        string key,
        string collectionName,
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organization.UseMyItems = true;
        orgUser.OrganizationId = organization.Id;

        SetupRepositoryMocks(sutProvider, organization, orgUser);

        // Mock positive validation result
        var validationRequest = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            OrganizationUserId = orgUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = collectionName,
            PerformedBy = null,
            OrganizationUser = orgUser,
            Organization = organization
        };
        sutProvider.GetDependency<IAutomaticallyConfirmOrganizationUsersValidator>()
            .ValidateAsync(Arg.Any<AutomaticallyConfirmOrganizationUserValidationRequest>())
            .Returns(Valid(validationRequest));

        // Mock enabled policy requirement
        var policyDetails = new PolicyDetails
        {
            OrganizationId = organization.Id,
            OrganizationUserId = orgUser.Id,
            IsProvider = false,
            OrganizationUserStatus = orgUser.Status,
            OrganizationUserType = orgUser.Type,
            PolicyType = PolicyType.OrganizationDataOwnership
        };
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(orgUser.UserId!.Value)
            .Returns(new OrganizationDataOwnershipPolicyRequirement(OrganizationDataOwnershipState.Enabled, [policyDetails]));

        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = orgUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = collectionName,
            PerformedBy = null
        };

        // Act
        await sutProvider.Sut.AutomaticallyConfirmOrganizationUserAsync(request);

        // Assert - Collection repository should be called
        await sutProvider.GetDependency<ICollectionRepository>()
            .Received(1)
            .CreateDefaultCollectionsAsync(
                organization.Id,
                Arg.Is<IEnumerable<Guid>>(ids => ids.Single() == orgUser.Id),
                collectionName);
    }

    [Theory, BitAutoData]
    public async Task AutomaticallyConfirmOrganizationUserAsync_UseMyItemsEnabled_PolicyDisabled_DoesNotCreateCollection(
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Accepted)] OrganizationUser orgUser,
        string key,
        string collectionName,
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organization.UseMyItems = true;
        orgUser.OrganizationId = organization.Id;

        SetupRepositoryMocks(sutProvider, organization, orgUser);

        // Mock positive validation result
        var validationRequest = new AutomaticallyConfirmOrganizationUserValidationRequest
        {
            OrganizationUserId = orgUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = collectionName,
            PerformedBy = null,
            OrganizationUser = orgUser,
            Organization = organization
        };
        sutProvider.GetDependency<IAutomaticallyConfirmOrganizationUsersValidator>()
            .ValidateAsync(Arg.Any<AutomaticallyConfirmOrganizationUserValidationRequest>())
            .Returns(Valid(validationRequest));

        // Mock disabled policy requirement
        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(orgUser.UserId!.Value)
            .Returns(new OrganizationDataOwnershipPolicyRequirement(OrganizationDataOwnershipState.Disabled, []));

        var request = new AutomaticallyConfirmOrganizationUserRequest
        {
            OrganizationUserId = orgUser.Id,
            OrganizationId = organization.Id,
            Key = key,
            DefaultUserCollectionName = collectionName,
            PerformedBy = null
        };

        // Act
        await sutProvider.Sut.AutomaticallyConfirmOrganizationUserAsync(request);

        // Assert - Collection repository should NOT be called when policy is disabled
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .CreateDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<string>());
    }

    private static void SetupRepositoryMocks(
        SutProvider<AutomaticallyConfirmOrganizationUserCommand> sutProvider,
        Organization organization,
        OrganizationUser organizationUser)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .ConfirmOrganizationUserAsync(Arg.Any<AcceptedOrganizationUserToConfirm>())
            .Returns(true);
    }
}
