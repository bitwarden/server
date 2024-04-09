using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Queries;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using Stripe;

using static Bit.Core.Billing.Utilities;

namespace Bit.Core.Billing.Commands.Implementations;

public class CreateCustomerCommand(
    IGlobalSettings globalSettings,
    ILogger<CreateCustomerCommand> logger,
    IOrganizationRepository organizationRepository,
    IStripeAdapter stripeAdapter,
    ISubscriberQueries subscriberQueries) : ICreateCustomerCommand
{
    public async Task CreateCustomer(
        Provider provider,
        Organization organization)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(organization);

        if (!string.IsNullOrEmpty(organization.GatewayCustomerId))
        {
            logger.LogWarning("Client organization ({ID}) already has a populated {FieldName}", organization.Id, nameof(organization.GatewayCustomerId));

            return;
        }

        var providerCustomer = await subscriberQueries.GetCustomerOrThrow(provider, new CustomerGetOptions
        {
            Expand = ["tax_ids"]
        });

        var providerTaxId = providerCustomer.TaxIds.FirstOrDefault();

        var organizationOwnerEmail = (await organizationRepository.GetOwnerEmailAddressesById(organization.Id))
            .MinBy(email => email);

        if (string.IsNullOrEmpty(organizationOwnerEmail))
        {
            logger.LogError("Cannot create a Stripe customer for a client organization ({OrganizationID}) that does not have an owner", organization.Id);

            throw ContactSupport();
        }

        var organizationDisplayName = organization.DisplayName();

        var customerCreateOptions = new CustomerCreateOptions
        {
            Address = new AddressOptions
            {
                Country = providerCustomer.Address?.Country,
                PostalCode = providerCustomer.Address?.PostalCode,
                Line1 = providerCustomer.Address?.Line1,
                Line2 = providerCustomer.Address?.Line2,
                City = providerCustomer.Address?.City,
                State = providerCustomer.Address?.State
            },
            Description = organization.DisplayBusinessName(),
            Email = organizationOwnerEmail,
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                CustomFields =
                [
                    new CustomerInvoiceSettingsCustomFieldOptions
                    {
                        Name = organization.SubscriberType(),
                        Value = organizationDisplayName.Length <= 30
                            ? organizationDisplayName
                            : organizationDisplayName[..30]
                    }
                ]
            },
            Metadata = new Dictionary<string, string>
            {
                { "region", globalSettings.BaseServiceUri.CloudRegion }
            },
            TaxIdData = providerTaxId == null ? [] :
            [
                new CustomerTaxIdDataOptions
                {
                    Type = providerTaxId.Type,
                    Value = providerTaxId.Value
                }
            ]
        };

        await stripeAdapter.CustomerCreateAsync(customerCreateOptions);
    }
}
