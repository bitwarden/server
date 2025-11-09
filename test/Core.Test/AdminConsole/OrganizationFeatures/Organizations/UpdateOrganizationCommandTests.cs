using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Organizations.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class UpdateOrganizationCommandTests
{
    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenValidOrganization_UpdatesOrganization(
        Guid organizationId,
        string name,
        string billingEmail,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var organizationBillingService = sutProvider.GetDependency<IOrganizationBillingService>();

        organization.Id = organizationId;
        organization.GatewayCustomerId = null; // No Stripe customer, so no billing update

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        providerRepository
            .GetByOrganizationIdAsync(organizationId)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
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
            .DidNotReceiveWithAnyArgs()
            .UpdateOrganizationNameAndEmail(Arg.Any<Organization>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenNameChanges_UpdatesBilling(
        Guid organizationId,
        string newName,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationBillingService = sutProvider.GetDependency<IOrganizationBillingService>();

        organization.Id = organizationId;
        organization.Name = "Old Name";

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        providerRepository
            .GetByOrganizationIdAsync(organizationId)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            OrganizationId = organizationId,
            Name = newName,
            BillingEmail = organization.BillingEmail
        };

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(organizationId, result.Id);
        Assert.Equal(newName, result.Name);

        await organizationBillingService
            .Received(1)
            .UpdateOrganizationNameAndEmail(result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenBillingEmailChanges_UpdatesBilling(
        Guid organizationId,
        string newBillingEmail,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationBillingService = sutProvider.GetDependency<IOrganizationBillingService>();

        organization.Id = organizationId;
        organization.BillingEmail = "old@example.com";

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        providerRepository
            .GetByOrganizationIdAsync(organizationId)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            OrganizationId = organizationId,
            Name = organization.Name,
            BillingEmail = newBillingEmail
        };

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(organizationId, result.Id);
        Assert.Equal(newBillingEmail.ToLowerInvariant().Trim(), result.BillingEmail);

        await organizationBillingService
            .Received(1)
            .UpdateOrganizationNameAndEmail(result);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenProviderManaged_PreservesBillingEmail(
        Guid organizationId,
        string name,
        string newBillingEmail,
        Organization organization,
        Provider provider,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organization.Id = organizationId;
        organization.BillingEmail = "original@example.com";

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        providerRepository
            .GetByOrganizationIdAsync(organizationId)
            .Returns(provider);

        var request = new UpdateOrganizationRequest
        {
            OrganizationId = organizationId,
            Name = name,
            BillingEmail = newBillingEmail
        };

        // Act
        var result = await sutProvider.Sut.UpdateAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(organizationId, result.Id);
        Assert.Equal(name, result.Name);
        Assert.Equal("original@example.com", result.BillingEmail); // Original billing email preserved

        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                result,
                EventType.Organization_Updated);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenOrganizationNotFound_ThrowsNotFoundException(
        Guid organizationId,
        string name,
        string billingEmail,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns((Organization)null);

        var request = new UpdateOrganizationRequest
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
    public async Task UpdateAsync_WhenGatewayCustomerIdIsNullOrEmpty_SkipsBillingUpdate(
        string gatewayCustomerId,
        Guid organizationId,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var organizationBillingService = sutProvider.GetDependency<IOrganizationBillingService>();

        organization.Id = organizationId;
        organization.Name = "Old Name";
        organization.GatewayCustomerId = gatewayCustomerId;

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        providerRepository
            .GetByOrganizationIdAsync(organizationId)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
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
            .DidNotReceiveWithAnyArgs()
            .UpdateOrganizationNameAndEmail(Arg.Any<Organization>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenKeysProvided_AndNotAlreadySet_SetsKeys(
        Guid organizationId,
        string publicKey,
        string encryptedPrivateKey,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organization.Id = organizationId;
        organization.PublicKey = null;
        organization.PrivateKey = null;

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        providerRepository
            .GetByOrganizationIdAsync(organizationId)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            OrganizationId = organizationId,
            Name = organization.Name,
            BillingEmail = organization.BillingEmail,
            PublicKey = publicKey,
            EncryptedPrivateKey = encryptedPrivateKey
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
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organization.Id = organizationId;
        var existingPublicKey = organization.PublicKey;
        var existingPrivateKey = organization.PrivateKey;

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        providerRepository
            .GetByOrganizationIdAsync(organizationId)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            OrganizationId = organizationId,
            Name = organization.Name,
            BillingEmail = organization.BillingEmail,
            PublicKey = newPublicKey,
            EncryptedPrivateKey = newEncryptedPrivateKey
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
}
