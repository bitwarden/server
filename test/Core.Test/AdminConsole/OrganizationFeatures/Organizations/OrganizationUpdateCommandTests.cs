using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Update;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class OrganizationUpdateCommandTests
{
    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenValidOrganization_UpdatesOrganization(
        Guid organizationId,
        string name,
        string billingEmail,
        Organization organization,
        SutProvider<OrganizationUpdateCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var organizationBillingService = sutProvider.GetDependency<IOrganizationBillingService>();

        organization.Id = organizationId;
        organization.GatewayCustomerId = null; // No Stripe customer, but billing update is still called

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var request = new OrganizationUpdateRequest
        {
            OrganizationId = organizationId,
            Name = name,
            BillingEmail = billingEmail
        };

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(organizationId, result.Id);
        Assert.Equal(name, result.Name);
        Assert.Equal(billingEmail.ToLowerInvariant().Trim(), result.BillingEmail);

        await organizationRepository
            .Received(1)
            .GetByIdAsync(Arg.Is<Guid>(id => id == organizationId));
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                result,
                EventType.Organization_Updated);
        await organizationBillingService
            .Received(1)
            .UpdateOrganizationNameAndEmail(result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenOrganizationNotFound_ThrowsNotFoundException(
        Guid organizationId,
        string name,
        string billingEmail,
        SutProvider<OrganizationUpdateCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns((Organization)null);

        var request = new OrganizationUpdateRequest
        {
            OrganizationId = organizationId,
            Name = name,
            BillingEmail = billingEmail
        };

        // Act/Assert
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.UpdateAsync(request));
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData((string)null)]
    public async Task UpdateAsync_WhenGatewayCustomerIdIsNullOrEmpty_CallsBillingUpdateButHandledGracefully(
        string gatewayCustomerId,
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationUpdateCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var organizationBillingService = sutProvider.GetDependency<IOrganizationBillingService>();

        organization.Id = organizationId;
        organization.Name = "Old Name";
        organization.GatewayCustomerId = gatewayCustomerId;

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var request = new OrganizationUpdateRequest
        {
            OrganizationId = organizationId,
            Name = "New Name",
            BillingEmail = organization.BillingEmail
        };

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(organizationId, result.Id);
        Assert.Equal("New Name", result.Name);

        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                result,
                EventType.Organization_Updated);
        await organizationBillingService
            .Received(1)
            .UpdateOrganizationNameAndEmail(result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenKeysProvided_AndNotAlreadySet_SetsKeys(
        Guid organizationId,
        string publicKey,
        string encryptedPrivateKey,
        Organization organization,
        SutProvider<OrganizationUpdateCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organization.Id = organizationId;
        organization.PublicKey = null;
        organization.PrivateKey = null;

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var request = new OrganizationUpdateRequest
        {
            OrganizationId = organizationId,
            Name = organization.Name,
            BillingEmail = organization.BillingEmail,
            Keys = new PublicKeyEncryptionKeyPairData(
                wrappedPrivateKey: encryptedPrivateKey,
                publicKey: publicKey)
        };

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(organizationId, result.Id);
        Assert.Equal(publicKey, result.PublicKey);
        Assert.Equal(encryptedPrivateKey, result.PrivateKey);

        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                result,
                EventType.Organization_Updated);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenKeysProvided_AndAlreadySet_DoesNotOverwriteKeys(
        Guid organizationId,
        string newPublicKey,
        string newEncryptedPrivateKey,
        Organization organization,
        SutProvider<OrganizationUpdateCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organization.Id = organizationId;
        var existingPublicKey = organization.PublicKey;
        var existingPrivateKey = organization.PrivateKey;

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var request = new OrganizationUpdateRequest
        {
            OrganizationId = organizationId,
            Name = organization.Name,
            BillingEmail = organization.BillingEmail,
            Keys = new PublicKeyEncryptionKeyPairData(
                wrappedPrivateKey: newEncryptedPrivateKey,
                publicKey: newPublicKey)
        };

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(organizationId, result.Id);
        Assert.Equal(existingPublicKey, result.PublicKey);
        Assert.Equal(existingPrivateKey, result.PrivateKey);

        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                result,
                EventType.Organization_Updated);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_UpdatingNameOnly_UpdatesNameAndNotBillingEmail(
        Guid organizationId,
        string newName,
        Organization organization,
        SutProvider<OrganizationUpdateCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var organizationBillingService = sutProvider.GetDependency<IOrganizationBillingService>();

        organization.Id = organizationId;
        organization.Name = "Old Name";
        var originalBillingEmail = organization.BillingEmail;

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var request = new OrganizationUpdateRequest
        {
            OrganizationId = organizationId,
            Name = newName,
            BillingEmail = null
        };

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(organizationId, result.Id);
        Assert.Equal(newName, result.Name);
        Assert.Equal(originalBillingEmail, result.BillingEmail);

        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                result,
                EventType.Organization_Updated);
        await organizationBillingService
            .Received(1)
            .UpdateOrganizationNameAndEmail(result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_UpdatingBillingEmailOnly_UpdatesBillingEmailAndNotName(
        Guid organizationId,
        string newBillingEmail,
        Organization organization,
        SutProvider<OrganizationUpdateCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var organizationBillingService = sutProvider.GetDependency<IOrganizationBillingService>();

        organization.Id = organizationId;
        organization.BillingEmail = "old@example.com";
        var originalName = organization.Name;

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var request = new OrganizationUpdateRequest
        {
            OrganizationId = organizationId,
            Name = null,
            BillingEmail = newBillingEmail
        };

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(organizationId, result.Id);
        Assert.Equal(originalName, result.Name);
        Assert.Equal(newBillingEmail.ToLowerInvariant().Trim(), result.BillingEmail);

        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                result,
                EventType.Organization_Updated);
        await organizationBillingService
            .Received(1)
            .UpdateOrganizationNameAndEmail(result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenNoChanges_PreservesBothFields(
        Guid organizationId,
        Organization organization,
        SutProvider<OrganizationUpdateCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var organizationBillingService = sutProvider.GetDependency<IOrganizationBillingService>();

        organization.Id = organizationId;
        var originalName = organization.Name;
        var originalBillingEmail = organization.BillingEmail;

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        var request = new OrganizationUpdateRequest
        {
            OrganizationId = organizationId,
            Name = null,
            BillingEmail = null
        };

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(organizationId, result.Id);
        Assert.Equal(originalName, result.Name);
        Assert.Equal(originalBillingEmail, result.BillingEmail);

        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                result,
                EventType.Organization_Updated);
        await organizationBillingService
            .DidNotReceiveWithAnyArgs()
            .UpdateOrganizationNameAndEmail(Arg.Any<Organization>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_SelfHosted_OnlyUpdatesKeysNotOrganizationDetails(
        Guid organizationId,
        string newName,
        string newBillingEmail,
        string publicKey,
        string encryptedPrivateKey,
        Organization organization,
        SutProvider<OrganizationUpdateCommand> sutProvider)
    {
        // Arrange
        var organizationBillingService = sutProvider.GetDependency<IOrganizationBillingService>();
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();

        globalSettings.SelfHosted.Returns(true);

        organization.Id = organizationId;
        organization.Name = "Original Name";
        organization.BillingEmail = "original@example.com";
        organization.PublicKey = null;
        organization.PrivateKey = null;

        organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        var request = new OrganizationUpdateRequest
        {
            OrganizationId = organizationId,
            Name = newName, // Should be ignored
            BillingEmail = newBillingEmail, // Should be ignored
            Keys = new PublicKeyEncryptionKeyPairData(
                wrappedPrivateKey: encryptedPrivateKey,
                publicKey: publicKey)
        };

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.Equal("Original Name", result.Name); // Not changed
        Assert.Equal("original@example.com", result.BillingEmail); // Not changed
        Assert.Equal(publicKey, result.PublicKey); // Changed
        Assert.Equal(encryptedPrivateKey, result.PrivateKey); // Changed

        await organizationBillingService
            .DidNotReceiveWithAnyArgs()
            .UpdateOrganizationNameAndEmail(Arg.Any<Organization>());
    }
}
