using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Commands.Implementations;
using Bit.Core.Billing.Queries;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Stripe;
using Xunit;
using static Bit.Core.Test.Billing.Utilities;
using GlobalSettings = Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.Billing.Commands;

[SutProviderCustomize]
public class CreateCustomerCommandTests
{
    private const string _customerId = "customer_id";

    [Theory, BitAutoData]
    public async Task CreateCustomer_ForClientOrg_ProviderNull_ThrowsArgumentNullException(
        Organization organization,
        SutProvider<CreateCustomerCommand> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.CreateCustomer(null, organization));

    [Theory, BitAutoData]
    public async Task CreateCustomer_ForClientOrg_OrganizationNull_ThrowsArgumentNullException(
        Provider provider,
        SutProvider<CreateCustomerCommand> sutProvider) =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.CreateCustomer(provider, null));

    [Theory, BitAutoData]
    public async Task CreateCustomer_ForClientOrg_HasGatewayCustomerId_NoOp(
        Provider provider,
        Organization organization,
        SutProvider<CreateCustomerCommand> sutProvider)
    {
        organization.GatewayCustomerId = _customerId;

        await sutProvider.Sut.CreateCustomer(provider, organization);

        await sutProvider.GetDependency<ISubscriberQueries>().DidNotReceiveWithAnyArgs()
            .GetCustomerOrThrow(Arg.Any<ISubscriber>(), Arg.Any<CustomerGetOptions>());
    }

    [Theory, BitAutoData]
    public async Task CreateCustomer_ForClientOrg_NoOwnerEmails_ContactSupport(
        Provider provider,
        Organization organization,
        SutProvider<CreateCustomerCommand> sutProvider)
    {
        organization.GatewayCustomerId = null;

        var providerCustomer = new Customer
        {
            Address = new Address
            {
                Country = "USA",
                PostalCode = "12345",
                Line1 = "123 Main St.",
                Line2 = "Unit 4",
                City = "Fake Town",
                State = "Fake State"
            },
            TaxIds = new StripeList<TaxId>
            {
                Data =
                [
                    new TaxId { Type = "TYPE", Value = "VALUE" }
                ]
            }
        };

        sutProvider.GetDependency<ISubscriberQueries>().GetCustomerOrThrow(provider, Arg.Is<CustomerGetOptions>(
                options => options.Expand.FirstOrDefault() == "tax_ids"))
            .Returns(providerCustomer);

        sutProvider.GetDependency<IOrganizationRepository>().GetOwnerEmailAddressesById(organization.Id)
            .Returns([]);

        await ThrowsContactSupportAsync(() => sutProvider.Sut.CreateCustomer(provider, organization));

        await sutProvider.GetDependency<IStripeAdapter>().DidNotReceiveWithAnyArgs()
            .CustomerCreateAsync(Arg.Any<CustomerCreateOptions>());
    }

    [Theory, BitAutoData]
    public async Task CreateCustomer_ForClientOrg_Succeeds(
        Provider provider,
        Organization organization,
        SutProvider<CreateCustomerCommand> sutProvider)
    {
        organization.GatewayCustomerId = null;
        organization.Name = "Name";
        organization.BusinessName = "BusinessName";

        var providerCustomer = new Customer
        {
            Address = new Address
            {
                Country = "USA",
                PostalCode = "12345",
                Line1 = "123 Main St.",
                Line2 = "Unit 4",
                City = "Fake Town",
                State = "Fake State"
            },
            TaxIds = new StripeList<TaxId>
            {
                Data =
                [
                    new TaxId { Type = "TYPE", Value = "VALUE" }
                ]
            }
        };

        sutProvider.GetDependency<ISubscriberQueries>().GetCustomerOrThrow(provider, Arg.Is<CustomerGetOptions>(
                options => options.Expand.FirstOrDefault() == "tax_ids"))
            .Returns(providerCustomer);

        sutProvider.GetDependency<IGlobalSettings>().BaseServiceUri
            .Returns(new GlobalSettings.BaseServiceUriSettings(new GlobalSettings()) { CloudRegion = "US" });

        sutProvider.GetDependency<IOrganizationRepository>().GetOwnerEmailAddressesById(organization.Id)
            .Returns(["test@email.com"]);

        await sutProvider.Sut.CreateCustomer(provider, organization);

        await sutProvider.GetDependency<IStripeAdapter>().Received(1).CustomerCreateAsync(Arg.Is<CustomerCreateOptions>(
            options =>
                options.Address.Country == providerCustomer.Address.Country &&
                options.Address.PostalCode == providerCustomer.Address.PostalCode &&
                options.Address.Line1 == providerCustomer.Address.Line1 &&
                options.Address.Line2 == providerCustomer.Address.Line2 &&
                options.Address.City == providerCustomer.Address.City &&
                options.Address.State == providerCustomer.Address.State &&
                options.Description == "BusinessName" &&
                options.Email == "test@email.com" &&
                options.InvoiceSettings.CustomFields.FirstOrDefault().Name == "Organization" &&
                options.InvoiceSettings.CustomFields.FirstOrDefault().Value == "Name" &&
                options.Metadata["region"] == "US" &&
                options.TaxIdData.FirstOrDefault().Type == providerCustomer.TaxIds.FirstOrDefault().Type &&
                options.TaxIdData.FirstOrDefault().Value == providerCustomer.TaxIds.FirstOrDefault().Value));
    }
}
