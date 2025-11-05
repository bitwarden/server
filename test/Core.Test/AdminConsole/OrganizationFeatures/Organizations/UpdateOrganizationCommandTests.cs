using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
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
    public async Task UpdateAsync_WhenValidOrganization_AndUpdateBillingIsFalse_UpdatesOrganization(
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organizationRepository
            .GetByIdentifierAsync(organization.Identifier!)
            .Returns(organization);

        var request = new UpdateOrganizationRequest(organization, UpdateBilling: false);

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationRepository
            .Received(1)
            .GetByIdentifierAsync(Arg.Is<string>(id => id == organization.Identifier));
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org == organization),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenValidOrganization_AndUpdateBillingIsTrue_UpdatesOrganizationAndBilling(
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.GatewayCustomerId = "cus_test123";
        organization.BillingEmail = "billing@example.com";

        var expectedOptions = new CustomerUpdateOptions
        {
            Email = organization.BillingEmail,
            Description = organization.DisplayBusinessName(),
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                CustomFields = [
                    new CustomerInvoiceSettingsCustomFieldOptions
                    {
                        Name = organization.SubscriberType(),
                        Value = organization.DisplayName().Length <= 30
                            ? organization.DisplayName()
                            : organization.DisplayName()[..30]
                    }]
            },
        };

        organizationRepository
            .GetByIdentifierAsync(organization.Identifier!)
            .Returns(organization);

        var request = new UpdateOrganizationRequest(organization, UpdateBilling: true);

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationRepository
            .Received(1)
            .GetByIdentifierAsync(Arg.Is<string>(id => id == organization.Identifier));
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org == organization),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
        await stripeAdapter
            .Received(1)
            .CustomerUpdateAsync(
                Arg.Is<string>(id => id == organization.GatewayCustomerId),
                Arg.Is<CustomerUpdateOptions>(options =>
                    options.Email == expectedOptions.Email &&
                    options.Description == expectedOptions.Description &&
                    options.InvoiceSettings.CustomFields.First().Name == expectedOptions.InvoiceSettings.CustomFields.First().Name &&
                    options.InvoiceSettings.CustomFields.First().Value == expectedOptions.InvoiceSettings.CustomFields.First().Value));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenDisplayNameIsLong_TruncatesTo30Characters(
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.Name = "This is a very long organization name that exceeds thirty characters";
        organization.GatewayCustomerId = "cus_test123";

        organizationRepository
            .GetByIdentifierAsync(organization.Identifier!)
            .Returns(organization);

        var request = new UpdateOrganizationRequest(organization, UpdateBilling: true);

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
    public async Task UpdateAsync_WhenOrganizationHasNoId_ThrowsApplicationException(
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        organization.Id = Guid.Empty;
        var request = new UpdateOrganizationRequest(organization, UpdateBilling: false);

        // Act/Assert
        var exception = await Assert.ThrowsAsync<ApplicationException>(() => sutProvider.Sut.UpdateAsync(request));
        Assert.Equal("Cannot create org this way. Call SignUpAsync.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenIdentifierAlreadyExistsForDifferentOrganization_ThrowsBadRequestException(
        Organization organization,
        Organization existingOrganization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();

        // Set same identifier but different IDs
        existingOrganization.Identifier = organization.Identifier;
        existingOrganization.Id = Guid.NewGuid();

        organizationRepository
            .GetByIdentifierAsync(organization.Identifier!)
            .Returns(existingOrganization);

        var request = new UpdateOrganizationRequest(organization, UpdateBilling: false);

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
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organizationRepository
            .GetByIdentifierAsync(organization.Identifier!)
            .Returns(organization);

        var request = new UpdateOrganizationRequest(organization, UpdateBilling: false);

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org == organization),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenIdentifierIsNull_SkipsIdentifierCheck(
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organization.Identifier = null;

        var request = new UpdateOrganizationRequest(organization, UpdateBilling: false);

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationRepository
            .DidNotReceive()
            .GetByIdentifierAsync(Arg.Any<string>());
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org == organization),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenIdentifierIsEmpty_SkipsIdentifierCheck(
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organization.Identifier = string.Empty;

        var request = new UpdateOrganizationRequest(organization, UpdateBilling: false);

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationRepository
            .DidNotReceive()
            .GetByIdentifierAsync(Arg.Any<string>());
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org == organization),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WhenGatewayCustomerIdIsNull_SkipsBillingUpdate(
        Organization organization,
        SutProvider<UpdateOrganizationCommand> sutProvider)
    {
        // Arrange
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.GatewayCustomerId = null;

        organizationRepository
            .GetByIdentifierAsync(organization.Identifier!)
            .Returns(organization);

        var request = new UpdateOrganizationRequest(organization, UpdateBilling: true);

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org == organization),
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
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        var stripeAdapter = sutProvider.GetDependency<IStripeAdapter>();

        organization.GatewayCustomerId = string.Empty;

        organizationRepository
            .GetByIdentifierAsync(organization.Identifier!)
            .Returns(organization);

        var request = new UpdateOrganizationRequest(organization, UpdateBilling: true);

        // Act
        await sutProvider.Sut.UpdateAsync(request);

        // Assert
        await organizationService
            .Received(1)
            .ReplaceAndUpdateCacheAsync(
                Arg.Is<Organization>(org => org == organization),
                Arg.Is<EventType>(e => e == EventType.Organization_Updated));
        await stripeAdapter
            .DidNotReceiveWithAnyArgs()
            .CustomerUpdateAsync(Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }
}
