using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Stripe;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

/// <summary>
/// Request model for updating an organization.
/// </summary>
/// <param name="Organization">The organization entity to update.</param>
/// <param name="UpdateBilling">Whether to update the billing information in Stripe.</param>
public record UpdateOrganizationRequest(
    Organization Organization,
    bool UpdateBilling = false
);

public class UpdateOrganizationCommand(
    IOrganizationRepository organizationRepository,
    IStripeAdapter stripeAdapter,
    IOrganizationService organizationService
) : IUpdateOrganizationCommand
{
    public async Task UpdateAsync(UpdateOrganizationRequest request)
    {
        var organization = request.Organization;
        var updateBilling = request.UpdateBilling;

        if (organization.Id == default(Guid))
        {
            throw new ApplicationException("Cannot create org this way. Call SignUpAsync.");
        }

        if (!string.IsNullOrWhiteSpace(organization.Identifier))
        {
            var orgById = await organizationRepository.GetByIdentifierAsync(organization.Identifier);
            if (orgById != null && orgById.Id != organization.Id)
            {
                throw new BadRequestException("Identifier already in use by another organization.");
            }
        }

        await organizationService.ReplaceAndUpdateCacheAsync(organization, EventType.Organization_Updated);

        if (updateBilling && !string.IsNullOrWhiteSpace(organization.GatewayCustomerId))
        {
            var newDisplayName = organization.DisplayName();

            await stripeAdapter.CustomerUpdateAsync(organization.GatewayCustomerId,
                new CustomerUpdateOptions
                {
                    Email = organization.BillingEmail,
                    Description = organization.DisplayBusinessName(),
                    InvoiceSettings = new CustomerInvoiceSettingsOptions
                    {
                        // This overwrites the existing custom fields for this organization
                        CustomFields = [
                            new CustomerInvoiceSettingsCustomFieldOptions
                            {
                                Name = organization.SubscriberType(),
                                Value = newDisplayName.Length <= 30
                                    ? newDisplayName
                                    : newDisplayName[..30]
                            }]
                    },
                });
        }
    }
}
