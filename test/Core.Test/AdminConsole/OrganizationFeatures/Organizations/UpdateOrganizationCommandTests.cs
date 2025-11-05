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
        string name,
        string businessName,
        string billingEmail,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.Identifier = null;
        organization.GatewayCustomerId = null; // No Stripe customer, so no billing update

        providerRepository
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            Organization = organization,
            Name = name,
            BusinessName = businessName,
            BillingEmail = billingEmail
        };

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org =>
                    org.Id == organization.Id &&
                    org.Name == name &&
                    org.BusinessName == businessName &&
                    org.BillingEmail == billingEmail.ToLowerInvariant().Trim()),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenNameChanges_UpdatesStripe(
        string newName,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.Name = "Old Name";
        organization.BusinessName = "Business";
        organization.BillingEmail = "billing@example.com";
        organization.GatewayCustomerId = "cus_test123";
        organization.Identifier = null;

        providerRepository
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            Organization = organization,
            Name = newName,
            BusinessName = organization.BusinessName,
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
        string newBillingEmail,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.Name = "Organization Name";
        organization.BusinessName = "Business";
        organization.BillingEmail = "old@example.com";
        organization.GatewayCustomerId = "cus_test123";
        organization.Identifier = null;

        providerRepository
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            Organization = organization,
            Name = organization.Name,
            BusinessName = organization.BusinessName,
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
        string name,
        string businessName,
        string newBillingEmail,
        Organization organization,
        Provider provider,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organization.BillingEmail = "original@example.com";
        organization.Identifier = null;

        providerRepository
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(provider);

        var request = new UpdateOrganizationRequest
        {
            Organization = organization,
            Name = name,
            BusinessName = businessName,
            BillingEmail = newBillingEmail
        };

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org =>
                    org.Id == organization.Id &&
                    org.Name == name &&
                    org.BusinessName == businessName &&
                    org.BillingEmail == "original@example.com"), // Original billing email preserved
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenProviderManagedAndNameChanges_StillUpdatesStripe(
        string newName,
        Organization organization,
        Provider provider,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.Name = "Old Name";
        organization.BillingEmail = "billing@example.com";
        organization.GatewayCustomerId = "cus_test123";
        organization.Identifier = null;

        providerRepository
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(provider);

        var request = new UpdateOrganizationRequest
        {
            Organization = organization,
            Name = newName,
            BusinessName = organization.BusinessName,
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
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        var longName = "This is a very long organization name that exceeds thirty characters";

        organization.Name = "Old Name";
        organization.GatewayCustomerId = "cus_test123";
        organization.Identifier = null;

        providerRepository
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            Organization = organization,
            Name = longName,
            BusinessName = organization.BusinessName,
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
    public async Task UpdateAsync_WhenIdentifierAlreadyExistsForDifferentOrganization_ThrowsBadRequestException(
        Organization organization,
        Organization existingOrganization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();

        organization.Identifier = "test-identifier";

        // Set same identifier but different IDs
        existingOrganization.Identifier = organization.Identifier;
        existingOrganization.Id = Guid.NewGuid();

        providerRepository
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((Provider)null);

        organizationRepository
            .GetByIdentifierAsync(organization.Identifier!)
            .Returns(existingOrganization);

        var request = new UpdateOrganizationRequest
        {
            Organization = organization,
            Name = "New Name",
            BusinessName = organization.BusinessName,
            BillingEmail = organization.BillingEmail
        };

        // Act/Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateAsync(request));
        Assert.Equal("Identifier already in use by another organization.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenIdentifierBelongsToSameOrganization_UpdatesSuccessfully(
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organization.Identifier = "test-identifier";

        providerRepository
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((Provider)null);

        organizationRepository
            .GetByIdentifierAsync(organization.Identifier!)
            .Returns(organization);

        var request = new UpdateOrganizationRequest
        {
            Organization = organization,
            Name = "New Name",
            BusinessName = organization.BusinessName,
            BillingEmail = organization.BillingEmail
        };

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org.Id == organization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenIdentifierIsNull_SkipsIdentifierCheck(
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organization.Identifier = null;

        providerRepository
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            Organization = organization,
            Name = "New Name",
            BusinessName = organization.BusinessName,
            BillingEmail = organization.BillingEmail
        };

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationRepository
            .DidNotReceive()
            .GetByIdentifierAsync(Arg.Any<string>());
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org.Id == organization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenIdentifierIsEmpty_SkipsIdentifierCheck(
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organization.Identifier = string.Empty;

        providerRepository
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            Organization = organization,
            Name = "New Name",
            BusinessName = organization.BusinessName,
            BillingEmail = organization.BillingEmail
        };

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationRepository
            .DidNotReceive()
            .GetByIdentifierAsync(Arg.Any<string>());
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org.Id == organization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenGatewayCustomerIdIsNull_SkipsBillingUpdate(
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.Name = "Old Name";
        organization.GatewayCustomerId = null;
        organization.Identifier = null;

        providerRepository
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            Organization = organization,
            Name = "New Name",
            BusinessName = organization.BusinessName,
            BillingEmail = organization.BillingEmail
        };

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org.Id == organization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenGatewayCustomerIdIsEmpty_SkipsBillingUpdate(
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.Name = "Old Name";
        organization.GatewayCustomerId = string.Empty;
        organization.Identifier = null;

        providerRepository
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            Organization = organization,
            Name = "New Name",
            BusinessName = organization.BusinessName,
            BillingEmail = organization.BillingEmail
        };

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org.Id == organization.Id),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenKeysProvided_AndNotAlreadySet_SetsKeys(
        string publicKey,
        string encryptedPrivateKey,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organization.PublicKey = null;
        organization.PrivateKey = null;
        organization.Identifier = null;

        providerRepository
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            Organization = organization,
            Name = organization.Name,
            BusinessName = organization.BusinessName,
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
                    org.Id == organization.Id &&
                    org.PublicKey == publicKey &&
                    org.PrivateKey == encryptedPrivateKey),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenKeysProvided_AndAlreadySet_DoesNotOverwriteKeys(
        string newPublicKey,
        string newEncryptedPrivateKey,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        var existingPublicKey = organization.PublicKey;
        var existingPrivateKey = organization.PrivateKey;
        organization.Identifier = null;

        providerRepository
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            Organization = organization,
            Name = organization.Name,
            BusinessName = organization.BusinessName,
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
                    org.Id == organization.Id &&
                    org.PublicKey == existingPublicKey &&
                    org.PrivateKey == existingPrivateKey),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenOnlyPublicKeyProvided_SetsOnlyPublicKey(
        string publicKey,
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var providerRepository = sutProvider.GetDependency<IProviderRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organization.PublicKey = null;
        organization.PrivateKey = null;
        organization.Identifier = null;

        providerRepository
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((Provider)null);

        var request = new UpdateOrganizationRequest
        {
            Organization = organization,
            Name = organization.Name,
            BusinessName = organization.BusinessName,
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
                    org.Id == organization.Id &&
                    org.PublicKey == publicKey &&
                    org.PrivateKey == null),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
    }
}
