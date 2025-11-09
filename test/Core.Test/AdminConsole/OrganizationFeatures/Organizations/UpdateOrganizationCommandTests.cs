using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
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
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.Id = organizationId;
        organization.Identifier = null;
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
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationRepository
            .Received(1)
            .GetByIdAsync(Arg.Is<Guid>(id => id == organizationId));
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org =>
                    org.Id == organizationId &&
                    org.Name == name &&
                    org.BillingEmail == billingEmail.ToLowerInvariant().Trim()),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenNameChanges_UpdatesStripe(
        Guid organizationId,
        string newName,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.Id = organizationId;
        organization.Name = "Old Name";
        organization.BusinessName = "Business";
        organization.BillingEmail = "billing@example.com";
        organization.GatewayCustomerId = "cus_test123";
        organization.Identifier = null;

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
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await stripeAdapter
            .Received(1)
            .CustomerUpdateAsync(
                Arg.Is<string>(id => id == organization.GatewayCustomerId),
                Arg.Is<CustomerUpdateOptions>(options =>
                    options.Email == organization.BillingEmail &&
                    options.Description == organization.DisplayBusinessName()));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenBillingEmailChanges_UpdatesStripe(
        Guid organizationId,
        string newBillingEmail,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.Id = organizationId;
        organization.Name = "Organization Name";
        organization.BusinessName = "Business";
        organization.BillingEmail = "old@example.com";
        organization.GatewayCustomerId = "cus_test123";
        organization.Identifier = null;

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
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await stripeAdapter
            .Received(1)
            .CustomerUpdateAsync(
                Arg.Is<string>(id => id == organization.GatewayCustomerId),
                Arg.Is<CustomerUpdateOptions>(options =>
                    options.Email == newBillingEmail.ToLowerInvariant().Trim()));
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
        organization.Identifier = null;

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
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org =>
                    org.Id == organizationId &&
                    org.Name == name &&
                    org.BillingEmail == "original@example.com"), // Original billing email preserved
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenProviderManagedAndNameChanges_StillUpdatesStripe(
        Guid organizationId,
        string newName,
        Organization organization,
        Provider provider,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.Id = organizationId;
        organization.Name = "Old Name";
        organization.BillingEmail = "billing@example.com";
        organization.GatewayCustomerId = "cus_test123";
        organization.Identifier = null;

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        providerRepository
            .GetByOrganizationIdAsync(organizationId)
            .Returns(provider);

        var request = new UpdateOrganizationRequest
        {
            OrganizationId = organizationId,
            Name = newName,
            BillingEmail = "new@example.com" // This will be ignored due to provider
        };

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await stripeAdapter
            .Received(1)
            .CustomerUpdateAsync(
                Arg.Is<string>(id => id == organization.GatewayCustomerId),
                Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenDisplayNameIsLong_TruncatesTo30Characters(
        Guid organizationId,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        var longName = "This is a very long organization name that exceeds thirty characters";

        organization.Id = organizationId;
        organization.Name = "Old Name";
        organization.GatewayCustomerId = "cus_test123";
        organization.Identifier = null;

        organizationRepository
            .GetByIdAsync(organizationId)
            .Returns(organization);

        providerRepository
            .GetByOrganizationIdAsync(organizationId)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            OrganizationId = organizationId,
            Name = longName,
            BillingEmail = organization.BillingEmail
        };

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await stripeAdapter
            .Received(1)
            .CustomerUpdateAsync(
                Arg.Any<string>(),
                Arg.Is<CustomerUpdateOptions>(options =>
                    options.InvoiceSettings.CustomFields.First().Value.Length == 30));
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

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenGatewayCustomerIdIsNull_SkipsBillingUpdate(
        Guid organizationId,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.Id = organizationId;
        organization.Name = "Old Name";
        organization.GatewayCustomerId = null;
        organization.Identifier = null;

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
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org.Id == organizationId),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenGatewayCustomerIdIsEmpty_SkipsBillingUpdate(
        Guid organizationId,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.Id = organizationId;
        organization.Name = "Old Name";
        organization.GatewayCustomerId = string.Empty;
        organization.Identifier = null;

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
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org.Id == organizationId),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
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
        organization.Identifier = null;

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
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org =>
                    org.Id == organizationId &&
                    org.PublicKey == publicKey &&
                    org.PrivateKey == encryptedPrivateKey),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
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
        organization.Identifier = null;

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
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org =>
                    org.Id == organizationId &&
                    org.PublicKey == existingPublicKey &&
                    org.PrivateKey == existingPrivateKey),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenOnlyPublicKeyProvided_SetsOnlyPublicKey(
        Guid organizationId,
        string publicKey,
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
        organization.Identifier = null;

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
            EncryptedPrivateKey = null
        };

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org =>
                    org.Id == organizationId &&
                    org.PublicKey == publicKey &&
                    org.PrivateKey == null),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
    }
}
