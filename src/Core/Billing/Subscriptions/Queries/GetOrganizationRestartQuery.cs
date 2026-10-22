using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Subscriptions.Models;
using Bit.Core.Exceptions;
using Stripe;

namespace Bit.Core.Billing.Subscriptions.Queries;

public interface IGetOrganizationRestartQuery
{
    Task<OrganizationRestart> Run(Organization organization, Subscription canceledSubscription);
}

public class GetOrganizationRestartQuery(IPricingClient pricingClient) : IGetOrganizationRestartQuery
{
    public async Task<OrganizationRestart> Run(Organization organization, Subscription canceledSubscription)
    {
        var plans = await pricingClient.ListPlans();

        var oldPlan = plans.FirstOrDefault(plan => plan.Type == organization.PlanType);

        if (oldPlan == null)
        {
            throw new ConflictException("Could not find plan for organization's plan type");
        }

        var newPlan = oldPlan.Disabled
            ? plans.FirstOrDefault(plan =>
                plan.ProductTier == oldPlan.ProductTier &&
                plan.IsAnnual == oldPlan.IsAnnual &&
                !plan.Disabled)
            : oldPlan;

        if (newPlan == null)
        {
            throw new ConflictException("Could not find the current, enabled plan for organization's tier and cadence");
        }

        var items = new List<OrganizationRestartItem>();

        // Password Manager
        var passwordManagerItem = canceledSubscription.Items.FirstOrDefault(item =>
            item.Price.Id == (oldPlan.HasNonSeatBasedPasswordManagerPlan()
                ? oldPlan.PasswordManager.StripePlanId
                : oldPlan.PasswordManager.StripeSeatPlanId));

        if (passwordManagerItem == null)
        {
            throw new ConflictException("Organization's subscription does not have a Password Manager subscription item.");
        }

        items.Add(new OrganizationRestartItem(
            newPlan.HasNonSeatBasedPasswordManagerPlan() ? newPlan.PasswordManager.StripePlanId : newPlan.PasswordManager.StripeSeatPlanId,
            passwordManagerItem.Quantity));

        // Storage
        var storageItem = canceledSubscription.Items.FirstOrDefault(
            item => item.Price.Id == oldPlan.PasswordManager.StripeStoragePlanId);

        if (storageItem != null)
        {
            items.Add(new OrganizationRestartItem(newPlan.PasswordManager.StripeStoragePlanId, storageItem.Quantity));
        }

        // Secrets Manager & Service Accounts
        var secretsManagerItem = oldPlan.SecretsManager != null
            ? canceledSubscription.Items.FirstOrDefault(item =>
                item.Price.Id == oldPlan.SecretsManager.StripeSeatPlanId)
            : null;

        var serviceAccountsItem = oldPlan.SecretsManager != null
            ? canceledSubscription.Items.FirstOrDefault(item =>
                item.Price.Id == oldPlan.SecretsManager.StripeServiceAccountPlanId)
            : null;

        if (newPlan.SecretsManager != null)
        {
            if (secretsManagerItem != null)
            {
                items.Add(new OrganizationRestartItem(newPlan.SecretsManager.StripeSeatPlanId, secretsManagerItem.Quantity));
            }

            if (serviceAccountsItem != null)
            {
                items.Add(new OrganizationRestartItem(newPlan.SecretsManager.StripeServiceAccountPlanId, serviceAccountsItem.Quantity));
            }
        }

        return new OrganizationRestart(newPlan, items);
    }
}
