using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

[SutProviderCustomize]
public class OrganizationDataOwnershipPolicyValidatorTests
{
    private const string _defaultUserCollectionName = "Default";

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_FeatureFlagDisabled_DoesNothing(
        [PolicyUpdate(PolicyType.OrganizationDataOwnership)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.OrganizationDataOwnership, false)] Policy currentPolicy,
        SutProvider<OrganizationDataOwnershipPolicyValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
            .Returns(false);

        // Act
        await sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, currentPolicy);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .UpsertDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<List<Guid>>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_PolicyAlreadyEnabled_DoesNothing(
        [PolicyUpdate(PolicyType.OrganizationDataOwnership)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.OrganizationDataOwnership, true)] Policy currentPolicy,
        SutProvider<OrganizationDataOwnershipPolicyValidator> sutProvider)
    {
        // Arrange
        currentPolicy.OrganizationId = policyUpdate.OrganizationId;
        policyUpdate.Enabled = true;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
            .Returns(true);

        // Act
        await sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, currentPolicy);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .UpsertDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<List<Guid>>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_PolicyBeingDisabled_DoesNothing(
        [PolicyUpdate(PolicyType.OrganizationDataOwnership, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.OrganizationDataOwnership, true)] Policy currentPolicy,
        SutProvider<OrganizationDataOwnershipPolicyValidator> sutProvider)
    {
        // Arrange
        currentPolicy.OrganizationId = policyUpdate.OrganizationId;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
            .Returns(true);

        // Act
        await sutProvider.Sut.OnSaveSideEffectsAsync(policyUpdate, currentPolicy);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .UpsertDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<List<Guid>>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_WhenNoUsersExist_ShouldLogError(
        [PolicyUpdate(PolicyType.OrganizationDataOwnership)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.OrganizationDataOwnership, false)] Policy currentPolicy,
        OrganizationDataOwnershipPolicyRequirementFactory factory)
    {
        // Arrange
        currentPolicy.OrganizationId = policyUpdate.OrganizationId;
        policyUpdate.Enabled = true;

        var policyRepository = ArrangePolicyRepositoryWithOutUsers();
        var collectionRepository = Substitute.For<ICollectionRepository>();
        var logger = Substitute.For<ILogger<OrganizationDataOwnershipPolicyValidator>>();

        var sut = ArrangeSut(factory, policyRepository, collectionRepository, logger);

        // Act
        await sut.OnSaveSideEffectsAsync(policyUpdate, currentPolicy);

        // Assert
        await collectionRepository
            .DidNotReceive()
            .UpsertDefaultCollectionsAsync(
                Arg.Any<Guid>(),
                Arg.Any<List<Guid>>(),
                Arg.Any<string>());

        const string expectedErrorMessage = "No UserOrganizationIds found for";

        logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => (o.ToString() ?? "").Contains(expectedErrorMessage)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory, BitAutoData]
    public async Task OnSaveSideEffectsAsync_WithRequirements_ShouldUpsertDefaultCollections(
        [PolicyUpdate(PolicyType.OrganizationDataOwnership)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.OrganizationDataOwnership, false)] Policy currentPolicy,
        OrganizationDataOwnershipPolicyRequirementFactory factory)
    {
        // Arrange
        currentPolicy.OrganizationId = policyUpdate.OrganizationId;
        policyUpdate.Enabled = true;

        var policyRepository = ArrangePolicyRepositoryWithUsers(policyUpdate);
        var collectionRepository = Substitute.For<ICollectionRepository>();
        var logger = Substitute.For<ILogger<OrganizationDataOwnershipPolicyValidator>>();

        var sut = ArrangeSut(factory, policyRepository, collectionRepository, logger);

        // Act
        await sut.OnSaveSideEffectsAsync(policyUpdate, currentPolicy);

        // Assert
        await collectionRepository
            .Received(1)
            .UpsertDefaultCollectionsAsync(
                policyUpdate.OrganizationId,
                Arg.Is<List<Guid>>(ids => ids.Count == 3),
                _defaultUserCollectionName);
    }

    private static IPolicyRepository ArrangePolicyRepositoryWithUsers(PolicyUpdate policyUpdate)
    {
        var policyDetails = GenerateOrganizationPolicyDetails(policyUpdate);
        return ArrangePolicyRepository(policyDetails);
    }

    private static IPolicyRepository ArrangePolicyRepositoryWithOutUsers()
    {
        return ArrangePolicyRepository([]);
    }

    private static IPolicyRepository ArrangePolicyRepository(List<OrganizationPolicyDetails> policyDetails)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();

        policyRepository
            .GetPolicyDetailsByOrganizationIdAsync(Arg.Any<Guid>(), PolicyType.OrganizationDataOwnership)
            .Returns(policyDetails);
        return policyRepository;
    }

    private static OrganizationDataOwnershipPolicyValidator ArrangeSut(
        OrganizationDataOwnershipPolicyRequirementFactory factory,
        IPolicyRepository policyRepository,
        ICollectionRepository collectionRepository,
        ILogger<OrganizationDataOwnershipPolicyValidator> logger = null!)
    {
        logger ??= Substitute.For<ILogger<OrganizationDataOwnershipPolicyValidator>>();

        var featureService = Substitute.For<IFeatureService>();
        featureService
            .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
            .Returns(true);

        var sut = new OrganizationDataOwnershipPolicyValidator(policyRepository, collectionRepository, [factory], featureService, logger);
        return sut;
    }

    private static List<OrganizationPolicyDetails> GenerateOrganizationPolicyDetails(PolicyUpdate policyUpdate)
    {
        var policyDetails = new List<OrganizationPolicyDetails>
        {
            new()
            {
                OrganizationId = policyUpdate.OrganizationId,
                OrganizationUserId = Guid.NewGuid(),
                PolicyType = PolicyType.OrganizationDataOwnership,
                UserId = Guid.NewGuid(),
                PolicyData = "{}",
                OrganizationUserType = OrganizationUserType.User,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed
            },
            new()
            {
                OrganizationId = policyUpdate.OrganizationId,
                OrganizationUserId = Guid.NewGuid(),
                PolicyType = PolicyType.OrganizationDataOwnership,
                UserId = Guid.NewGuid(),
                PolicyData = "{}",
                OrganizationUserType = OrganizationUserType.User,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed
            },
            new()
            {
                OrganizationId = policyUpdate.OrganizationId,
                OrganizationUserId = Guid.NewGuid(),
                PolicyType = PolicyType.OrganizationDataOwnership,
                UserId = Guid.NewGuid(),
                PolicyData = "{}",
                OrganizationUserType = OrganizationUserType.User,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed
            }
        };
        return policyDetails;
    }
}
