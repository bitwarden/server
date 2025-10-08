using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

[SutProviderCustomize]
public class OrganizationDataOwnershipPolicyHandlerTests
{
    private const string _defaultUserCollectionName = "Default";

    [Theory, BitAutoData]
    public async Task ExecuteSideEffectsAsync_FeatureFlagDisabled_DoesNothing(
        [PolicyUpdate(PolicyType.OrganizationDataOwnership, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.OrganizationDataOwnership, false)] Policy postUpdatedPolicy,
        [Policy(PolicyType.OrganizationDataOwnership, false)] Policy previousPolicyState,
        SutProvider<OrganizationDataOwnershipPolicyHandler> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
            .Returns(false);

        var policyRequest = new SavePolicyModel(policyUpdate, null, new OrganizationModelOwnershipPolicyModel(_defaultUserCollectionName));

        // Act
        await sutProvider.Sut.ExecuteSideEffectsAsync(policyRequest, postUpdatedPolicy, previousPolicyState);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .UpsertDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ExecuteSideEffectsAsync_PolicyAlreadyEnabled_DoesNothing(
        [PolicyUpdate(PolicyType.OrganizationDataOwnership, true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.OrganizationDataOwnership, true)] Policy postUpdatedPolicy,
        [Policy(PolicyType.OrganizationDataOwnership, true)] Policy previousPolicyState,
        SutProvider<OrganizationDataOwnershipPolicyHandler> sutProvider)
    {
        // Arrange
        postUpdatedPolicy.OrganizationId = policyUpdate.OrganizationId;
        previousPolicyState.OrganizationId = policyUpdate.OrganizationId;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
            .Returns(true);

        var policyRequest = new SavePolicyModel(policyUpdate, null, new OrganizationModelOwnershipPolicyModel(_defaultUserCollectionName));

        // Act
        await sutProvider.Sut.ExecuteSideEffectsAsync(policyRequest, postUpdatedPolicy, previousPolicyState);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .UpsertDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ExecuteSideEffectsAsync_PolicyBeingDisabled_DoesNothing(
        [PolicyUpdate(PolicyType.OrganizationDataOwnership, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.OrganizationDataOwnership, false)] Policy postUpdatedPolicy,
        [Policy(PolicyType.OrganizationDataOwnership)] Policy previousPolicyState,
        SutProvider<OrganizationDataOwnershipPolicyHandler> sutProvider)
    {
        // Arrange
        previousPolicyState.OrganizationId = policyUpdate.OrganizationId;
        postUpdatedPolicy.OrganizationId = policyUpdate.OrganizationId;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
            .Returns(true);

        var policyRequest = new SavePolicyModel(policyUpdate, null, new OrganizationModelOwnershipPolicyModel(_defaultUserCollectionName));

        // Act
        await sutProvider.Sut.ExecuteSideEffectsAsync(policyRequest, postUpdatedPolicy, previousPolicyState);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .UpsertDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ExecuteSideEffectsAsync_WhenNoUsersExist_DoNothing(
        [PolicyUpdate(PolicyType.OrganizationDataOwnership, true)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.OrganizationDataOwnership, true)] Policy postUpdatedPolicy,
        [Policy(PolicyType.OrganizationDataOwnership, false)] Policy previousPolicyState,
        OrganizationDataOwnershipPolicyRequirementFactory factory)
    {
        // Arrange
        postUpdatedPolicy.OrganizationId = policyUpdate.OrganizationId;
        previousPolicyState.OrganizationId = policyUpdate.OrganizationId;

        var policyRepository = ArrangePolicyRepository([]);
        var collectionRepository = Substitute.For<ICollectionRepository>();

        var sut = ArrangeSut(factory, policyRepository, collectionRepository);
        var policyRequest = new SavePolicyModel(policyUpdate, null, new OrganizationModelOwnershipPolicyModel(_defaultUserCollectionName));

        // Act
        await sut.ExecuteSideEffectsAsync(policyRequest, postUpdatedPolicy, previousPolicyState);

        // Assert
        await collectionRepository
            .DidNotReceive()
            .UpsertDefaultCollectionsAsync(
                Arg.Any<Guid>(),
                Arg.Any<IEnumerable<Guid>>(),
                Arg.Any<string>());

        await policyRepository
            .Received(1)
            .GetPolicyDetailsByOrganizationIdAsync(
                policyUpdate.OrganizationId,
                PolicyType.OrganizationDataOwnership);
    }

    public static IEnumerable<object?[]> ShouldUpsertDefaultCollectionsTestCases()
    {
        yield return WithExistingPolicy();

        yield return WithNoExistingPolicy();
        yield break;

        object?[] WithExistingPolicy()
        {
            var organizationId = Guid.NewGuid();
            var postUpdatedPolicy = new Policy
            {
                OrganizationId = organizationId,
                Type = PolicyType.OrganizationDataOwnership,
                Enabled = true
            };
            var previousPolicyState = new Policy
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Type = PolicyType.OrganizationDataOwnership,
                Enabled = false
            };

            return new object?[]
            {
                postUpdatedPolicy,
                previousPolicyState
            };
        }

        object?[] WithNoExistingPolicy()
        {
            var postUpdatedPolicy = new Policy
            {
                OrganizationId = new Guid(),
                Type = PolicyType.OrganizationDataOwnership,
                Enabled = true
            };

            const Policy previousPolicyState = null;

            return new object?[]
            {
                postUpdatedPolicy,
                previousPolicyState
            };
        }
    }
    [Theory]
    [BitMemberAutoData(nameof(ShouldUpsertDefaultCollectionsTestCases))]
    public async Task ExecuteSideEffectsAsync_WithRequirements_ShouldUpsertDefaultCollections(
        Policy postUpdatedPolicy,
        Policy? previousPolicyState,
        [PolicyUpdate(PolicyType.OrganizationDataOwnership)] PolicyUpdate policyUpdate,
        [OrganizationPolicyDetails(PolicyType.OrganizationDataOwnership)] IEnumerable<OrganizationPolicyDetails> orgPolicyDetails,
        OrganizationDataOwnershipPolicyRequirementFactory factory)
    {
        // Arrange
        var orgPolicyDetailsList = orgPolicyDetails.ToList();
        foreach (var policyDetail in orgPolicyDetailsList)
        {
            policyDetail.OrganizationId = policyUpdate.OrganizationId;
        }

        var policyRepository = ArrangePolicyRepository(orgPolicyDetailsList);
        var collectionRepository = Substitute.For<ICollectionRepository>();

        var sut = ArrangeSut(factory, policyRepository, collectionRepository);
        var policyRequest = new SavePolicyModel(policyUpdate, null, new OrganizationModelOwnershipPolicyModel(_defaultUserCollectionName));

        // Act
        await sut.ExecuteSideEffectsAsync(policyRequest, postUpdatedPolicy, previousPolicyState);

        // Assert
        await collectionRepository
            .Received(1)
            .UpsertDefaultCollectionsAsync(
                policyUpdate.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == 3),
                _defaultUserCollectionName);
    }

    private static IEnumerable<object?[]> WhenDefaultCollectionsDoesNotExistTestCases()
    {
        yield return [new OrganizationModelOwnershipPolicyModel(null)];
        yield return [new OrganizationModelOwnershipPolicyModel("")];
        yield return [new OrganizationModelOwnershipPolicyModel("   ")];
        yield return [new EmptyMetadataModel()];
    }
    [Theory]
    [BitMemberAutoData(nameof(WhenDefaultCollectionsDoesNotExistTestCases))]
    public async Task ExecuteSideEffectsAsync_WhenDefaultCollectionNameIsInvalid_DoesNothing(
        IPolicyMetadataModel metadata,
        [PolicyUpdate(PolicyType.OrganizationDataOwnership)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.OrganizationDataOwnership, true)] Policy postUpdatedPolicy,
        [Policy(PolicyType.OrganizationDataOwnership, false)] Policy previousPolicyState,
        SutProvider<OrganizationDataOwnershipPolicyHandler> sutProvider)
    {
        // Arrange
        postUpdatedPolicy.OrganizationId = policyUpdate.OrganizationId;
        previousPolicyState.OrganizationId = policyUpdate.OrganizationId;
        policyUpdate.Enabled = true;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
            .Returns(true);

        var policyRequest = new SavePolicyModel(policyUpdate, null, metadata);

        // Act
        await sutProvider.Sut.ExecuteSideEffectsAsync(policyRequest, postUpdatedPolicy, previousPolicyState);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .UpsertDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<string>());
    }

    private static IPolicyRepository ArrangePolicyRepository(IEnumerable<OrganizationPolicyDetails> policyDetails)
    {
        var policyRepository = Substitute.For<IPolicyRepository>();

        policyRepository
            .GetPolicyDetailsByOrganizationIdAsync(Arg.Any<Guid>(), PolicyType.OrganizationDataOwnership)
            .Returns(policyDetails);
        return policyRepository;
    }

    private static OrganizationDataOwnershipPolicyHandler ArrangeSut(
        OrganizationDataOwnershipPolicyRequirementFactory factory,
        IPolicyRepository policyRepository,
        ICollectionRepository collectionRepository)
    {

        var featureService = Substitute.For<IFeatureService>();
        featureService
            .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
            .Returns(true);

        var sut = new OrganizationDataOwnershipPolicyHandler(policyRepository, collectionRepository, [factory], featureService);
        return sut;
    }

}
