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
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

[SutProviderCustomize]
public class OrganizationDataOwnershipPolicyValidatorTests
{
    private const string _defaultUserCollectionName = "Default";

    [Theory, BitAutoData]
    public async Task ExecuteSideEffectsAsync_FeatureFlagDisabled_DoesNothing(
        [PolicyUpdate(PolicyType.OrganizationDataOwnership)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.OrganizationDataOwnership, false)] Policy currentPolicy,
        SutProvider<OrganizationDataOwnershipPolicyValidator> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
            .Returns(false);

        var policyModel = new SavePolicyModel(policyUpdate, null, new OrganizationModelOwnershipPolicyModel(_defaultUserCollectionName));

        // Act
        await sutProvider.Sut.ExecuteSideEffectsAsync(policyModel, currentPolicy);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .UpsertDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<List<Guid>>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ExecuteSideEffectsAsync_PolicyAlreadyEnabled_DoesNothing(
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

        var policyModel = new SavePolicyModel(policyUpdate, null, new OrganizationModelOwnershipPolicyModel(_defaultUserCollectionName));

        // Act
        await sutProvider.Sut.ExecuteSideEffectsAsync(policyModel, currentPolicy);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .UpsertDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<List<Guid>>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ExecuteSideEffectsAsync_PolicyBeingDisabled_DoesNothing(
        [PolicyUpdate(PolicyType.OrganizationDataOwnership, false)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.OrganizationDataOwnership, true)] Policy currentPolicy,
        SutProvider<OrganizationDataOwnershipPolicyValidator> sutProvider)
    {
        // Arrange
        currentPolicy.OrganizationId = policyUpdate.OrganizationId;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
            .Returns(true);

        var policyModel = new SavePolicyModel(policyUpdate, null, new OrganizationModelOwnershipPolicyModel(_defaultUserCollectionName));

        // Act
        await sutProvider.Sut.ExecuteSideEffectsAsync(policyModel, currentPolicy);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .UpsertDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<List<Guid>>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task ExecuteSideEffectsAsync_WhenNoUsersExist_ShouldLogError(
        [PolicyUpdate(PolicyType.OrganizationDataOwnership)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.OrganizationDataOwnership, false)] Policy currentPolicy,
        OrganizationDataOwnershipPolicyRequirementFactory factory)
    {
        // Arrange
        currentPolicy.OrganizationId = policyUpdate.OrganizationId;
        policyUpdate.Enabled = true;

        var policyRepository = ArrangePolicyRepository([]);
        var collectionRepository = Substitute.For<ICollectionRepository>();
        var logger = Substitute.For<ILogger<OrganizationDataOwnershipPolicyValidator>>();

        var sut = ArrangeSut(factory, policyRepository, collectionRepository, logger);
        var policyModel = new SavePolicyModel(policyUpdate, null, new OrganizationModelOwnershipPolicyModel(_defaultUserCollectionName));

        // Act
        await sut.ExecuteSideEffectsAsync(policyModel, currentPolicy);

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

    public static IEnumerable<object?[]> ShouldUpsertDefaultCollectionsTestCases()
    {
        yield return WithExistingPolicy();

        yield return WithNoExistingPolicy();
        yield break;

        object?[] WithExistingPolicy()
        {
            var organizationId = Guid.NewGuid();
            var policyUpdate = new PolicyUpdate
            {
                OrganizationId = organizationId,
                Type = PolicyType.OrganizationDataOwnership,
                Enabled = true
            };
            var currentPolicy = new Policy
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Type = PolicyType.OrganizationDataOwnership,
                Enabled = false
            };

            return new object?[]
            {
                policyUpdate,
                currentPolicy
            };
        }

        object?[] WithNoExistingPolicy()
        {
            var policyUpdate = new PolicyUpdate
            {
                OrganizationId = new Guid(),
                Type = PolicyType.OrganizationDataOwnership,
                Enabled = true
            };

            const Policy currentPolicy = null;

            return new object?[]
            {
                policyUpdate,
                currentPolicy
            };
        }
    }
    [Theory, BitAutoData]
    [BitMemberAutoData(nameof(ShouldUpsertDefaultCollectionsTestCases))]
    public async Task ExecuteSideEffectsAsync_WithRequirements_ShouldUpsertDefaultCollections(
        [PolicyUpdate(PolicyType.OrganizationDataOwnership)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.OrganizationDataOwnership, false)] Policy? currentPolicy,
        [OrganizationPolicyDetails(PolicyType.OrganizationDataOwnership)] IEnumerable<OrganizationPolicyDetails> orgPolicyDetails,
        OrganizationDataOwnershipPolicyRequirementFactory factory)
    {
        // Arrange
        foreach (var policyDetail in orgPolicyDetails)
        {
            policyDetail.OrganizationId = policyUpdate.OrganizationId;
        }

        var policyRepository = ArrangePolicyRepository(orgPolicyDetails);
        var collectionRepository = Substitute.For<ICollectionRepository>();
        var logger = Substitute.For<ILogger<OrganizationDataOwnershipPolicyValidator>>();

        var sut = ArrangeSut(factory, policyRepository, collectionRepository, logger);
        var policyModel = new SavePolicyModel(policyUpdate, null, new OrganizationModelOwnershipPolicyModel(_defaultUserCollectionName));

        // Act
        await sut.ExecuteSideEffectsAsync(policyModel, currentPolicy);

        // Assert
        await collectionRepository
            .Received(1)
            .UpsertDefaultCollectionsAsync(
                policyUpdate.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == 3),
                _defaultUserCollectionName);
    }

    [Theory, BitAutoData]
    public async Task ExecuteSideEffectsAsync_WhenMetadataIsNull_DoesNothing(
    [PolicyUpdate(PolicyType.OrganizationDataOwnership)] PolicyUpdate policyUpdate,
    [Policy(PolicyType.OrganizationDataOwnership, false)] Policy currentPolicy,
    SutProvider<OrganizationDataOwnershipPolicyValidator> sutProvider)
    {
        // Arrange
        currentPolicy.OrganizationId = policyUpdate.OrganizationId;
        policyUpdate.Enabled = true;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
            .Returns(true);

        var policyModel = new SavePolicyModel(policyUpdate, null, new EmptyMetadataModel());

        // Act
        await sutProvider.Sut.ExecuteSideEffectsAsync(policyModel, currentPolicy);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .UpsertDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<List<Guid>>(), Arg.Any<string>());
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
    public async Task ExecuteSideEffectsAsync_WhenDefaultCollectionsDoesNotExist_DoesNothing(
        IPolicyMetadataModel metadata,
        [PolicyUpdate(PolicyType.OrganizationDataOwnership)] PolicyUpdate policyUpdate,
        [Policy(PolicyType.OrganizationDataOwnership, false)] Policy currentPolicy,
        SutProvider<OrganizationDataOwnershipPolicyValidator> sutProvider)
    {
        // Arrange
        currentPolicy.OrganizationId = policyUpdate.OrganizationId;
        policyUpdate.Enabled = true;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.CreateDefaultLocation)
            .Returns(true);

        var policyModel = new SavePolicyModel(policyUpdate, null, metadata);

        // Act
        await sutProvider.Sut.ExecuteSideEffectsAsync(policyModel, currentPolicy);

        // Assert
        await sutProvider.GetDependency<ICollectionRepository>()
            .DidNotReceive()
            .UpsertDefaultCollectionsAsync(Arg.Any<Guid>(), Arg.Any<List<Guid>>(), Arg.Any<string>());
    }

    private static IPolicyRepository ArrangePolicyRepository(IEnumerable<OrganizationPolicyDetails> policyDetails)
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

}
