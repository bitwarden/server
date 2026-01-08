#nullable enable
using System.Diagnostics.CodeAnalysis;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Providers.Entities;
using Bit.Core.Billing.Providers.Repositories;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using OneOf;
using Stripe;

namespace Bit.Commercial.Core.Billing.Providers.Services;

public class BusinessUnitConverter(
    IDataProtectionProvider dataProtectionProvider,
    GlobalSettings globalSettings,
    ILogger<BusinessUnitConverter> logger,
    IMailService mailService,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IPricingClient pricingClient,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderPlanRepository providerPlanRepository,
    IProviderRepository providerRepository,
    IProviderUserRepository providerUserRepository,
    IStripeAdapter stripeAdapter,
    ISubscriberService subscriberService,
    IUserRepository userRepository) : IBusinessUnitConverter
{
    private readonly IDataProtector _dataProtector =
        dataProtectionProvider.CreateProtector($"{nameof(BusinessUnitConverter)}DataProtector");

    public async Task<Guid> FinalizeConversion(
        Organization organization,
        Guid userId,
        string token,
        string providerKey,
        string organizationKey)
    {
        var user = await userRepository.GetByIdAsync(userId);

        var (subscription, provider, providerOrganization, providerUser) = await ValidateFinalizationAsync(organization, user, token);

        var existingPlan = await pricingClient.GetPlanOrThrow(organization.PlanType);
        var updatedPlan = await pricingClient.GetPlanOrThrow(existingPlan.IsAnnual ? PlanType.EnterpriseAnnually : PlanType.EnterpriseMonthly);

        // Bring organization under management.
        organization.Plan = updatedPlan.Name;
        organization.PlanType = updatedPlan.Type;
        organization.MaxCollections = updatedPlan.PasswordManager.MaxCollections;
        organization.MaxStorageGb = updatedPlan.PasswordManager.BaseStorageGb;
        organization.UsePolicies = updatedPlan.HasPolicies;
        organization.UseSso = updatedPlan.HasSso;
        organization.UseOrganizationDomains = updatedPlan.HasOrganizationDomains;
        organization.UseGroups = updatedPlan.HasGroups;
        organization.UseEvents = updatedPlan.HasEvents;
        organization.UseDirectory = updatedPlan.HasDirectory;
        organization.UseTotp = updatedPlan.HasTotp;
        organization.Use2fa = updatedPlan.Has2fa;
        organization.UseApi = updatedPlan.HasApi;
        organization.UseResetPassword = updatedPlan.HasResetPassword;
        organization.SelfHost = updatedPlan.HasSelfHost;
        organization.UsersGetPremium = updatedPlan.UsersGetPremium;
        organization.UseCustomPermissions = updatedPlan.HasCustomPermissions;
        organization.UseScim = updatedPlan.HasScim;
        organization.UseKeyConnector = updatedPlan.HasKeyConnector;
        organization.MaxStorageGb = updatedPlan.PasswordManager.BaseStorageGb;
        organization.BillingEmail = provider.BillingEmail!;
        organization.GatewayCustomerId = null;
        organization.GatewaySubscriptionId = null;
        organization.ExpirationDate = null;
        organization.MaxAutoscaleSeats = null;
        organization.Status = OrganizationStatusType.Managed;

        // Enable organization access via key exchange.
        providerOrganization.Key = organizationKey;

        // Complete provider setup.
        provider.Gateway = GatewayType.Stripe;
        provider.GatewayCustomerId = subscription.CustomerId;
        provider.GatewaySubscriptionId = subscription.Id;
        provider.Status = ProviderStatusType.Billable;

        // Enable provider access via key exchange.
        providerUser.Key = providerKey;
        providerUser.Status = ProviderUserStatusType.Confirmed;

        // Stripe requires that we clear all the custom fields from the invoice settings if we want to replace them.
        await stripeAdapter.UpdateCustomerAsync(subscription.CustomerId, new CustomerUpdateOptions
        {
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                CustomFields = []
            }
        });

        var metadata = new Dictionary<string, string>
        {
            [StripeConstants.MetadataKeys.OrganizationId] = string.Empty,
            [StripeConstants.MetadataKeys.ProviderId] = provider.Id.ToString(),
            ["convertedFrom"] = organization.Id.ToString()
        };

        var updateCustomer = stripeAdapter.UpdateCustomerAsync(subscription.CustomerId, new CustomerUpdateOptions
        {
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                CustomFields = [
                    new CustomerInvoiceSettingsCustomFieldOptions
                    {
                        Name = provider.SubscriberType(),
                        Value = provider.DisplayName()?.Length <= 30
                            ? provider.DisplayName()
                            : provider.DisplayName()?[..30]
                    }
                ]
            },
            Metadata = metadata
        });

        // Find the existing password manager price on the subscription.
        var passwordManagerItem = subscription.Items.First(item =>
        {
            var priceId = existingPlan.HasNonSeatBasedPasswordManagerPlan()
                ? existingPlan.PasswordManager.StripePlanId
                : existingPlan.PasswordManager.StripeSeatPlanId;

            return item.Price.Id == priceId;
        });

        // Get the new business unit price.
        var updatedPriceId = ProviderPriceAdapter.GetActivePriceId(provider, updatedPlan.Type);

        // Replace the existing password manager price with the new business unit price.
        var updateSubscription =
            stripeAdapter.UpdateSubscriptionAsync(subscription.Id,
                new SubscriptionUpdateOptions
                {
                    Items = [
                        new SubscriptionItemOptions
                        {
                            Id = passwordManagerItem.Id,
                            Deleted = true
                        },
                        new SubscriptionItemOptions
                        {
                            Price = updatedPriceId,
                            Quantity = organization.Seats
                        }
                    ],
                    Metadata = metadata
                });

        await Task.WhenAll(updateCustomer, updateSubscription);

        // Complete database updates for provider setup.
        await Task.WhenAll(
            organizationRepository.ReplaceAsync(organization),
            providerOrganizationRepository.ReplaceAsync(providerOrganization),
            providerRepository.ReplaceAsync(provider),
            providerUserRepository.ReplaceAsync(providerUser));

        return provider.Id;
    }

    public async Task<OneOf<Guid, List<string>>> InitiateConversion(
        Organization organization,
        string providerAdminEmail)
    {
        var user = await userRepository.GetByEmailAsync(providerAdminEmail);

        var problems = await ValidateInitiationAsync(organization, user);

        if (problems is { Count: > 0 })
        {
            return problems;
        }

        var provider = await providerRepository.CreateAsync(new Provider
        {
            Name = organization.Name,
            BillingEmail = organization.BillingEmail,
            Status = ProviderStatusType.Pending,
            UseEvents = true,
            Type = ProviderType.BusinessUnit
        });

        var plan = await pricingClient.GetPlanOrThrow(organization.PlanType);

        var managedPlanType = plan.IsAnnual
            ? PlanType.EnterpriseAnnually
            : PlanType.EnterpriseMonthly;

        var createProviderOrganization = providerOrganizationRepository.CreateAsync(new ProviderOrganization
        {
            ProviderId = provider.Id,
            OrganizationId = organization.Id
        });

        var createProviderPlan = providerPlanRepository.CreateAsync(new ProviderPlan
        {
            ProviderId = provider.Id,
            PlanType = managedPlanType,
            SeatMinimum = 0,
            PurchasedSeats = organization.Seats,
            AllocatedSeats = organization.Seats
        });

        var createProviderUser = providerUserRepository.CreateAsync(new ProviderUser
        {
            ProviderId = provider.Id,
            UserId = user!.Id,
            Email = user.Email,
            Status = ProviderUserStatusType.Invited,
            Type = ProviderUserType.ProviderAdmin
        });

        await Task.WhenAll(createProviderOrganization, createProviderPlan, createProviderUser);

        await SendInviteAsync(organization, user.Email);

        return provider.Id;
    }

    public Task ResendConversionInvite(
        Organization organization,
        string providerAdminEmail) =>
        IfConversionInProgressAsync(organization, providerAdminEmail,
            async (_, _, providerUser) =>
            {
                if (!string.IsNullOrEmpty(providerUser.Email))
                {
                    await SendInviteAsync(organization, providerUser.Email);
                }
            });

    public Task ResetConversion(
        Organization organization,
        string providerAdminEmail) =>
        IfConversionInProgressAsync(organization, providerAdminEmail,
            async (provider, providerOrganization, providerUser) =>
            {
                var tasks = new List<Task>
                {
                    providerOrganizationRepository.DeleteAsync(providerOrganization),
                    providerUserRepository.DeleteAsync(providerUser)
                };

                var providerPlans = await providerPlanRepository.GetByProviderId(provider.Id);

                if (providerPlans is { Count: > 0 })
                {
                    tasks.AddRange(providerPlans.Select(providerPlanRepository.DeleteAsync));
                }

                await Task.WhenAll(tasks);

                await providerRepository.DeleteAsync(provider);
            });

    #region Utilities

    private async Task IfConversionInProgressAsync(
        Organization organization,
        string providerAdminEmail,
        Func<Provider, ProviderOrganization, ProviderUser, Task> callback)
    {
        var user = await userRepository.GetByEmailAsync(providerAdminEmail);

        if (user == null)
        {
            return;
        }

        var provider = await providerRepository.GetByOrganizationIdAsync(organization.Id);

        if (provider is not
            {
                Type: ProviderType.BusinessUnit,
                Status: ProviderStatusType.Pending
            })
        {
            return;
        }

        var providerUser = await providerUserRepository.GetByProviderUserAsync(provider.Id, user.Id);

        if (providerUser is
            {
                Type: ProviderUserType.ProviderAdmin,
                Status: ProviderUserStatusType.Invited
            })
        {
            var providerOrganization = await providerOrganizationRepository.GetByOrganizationId(organization.Id);
            await callback(provider, providerOrganization!, providerUser);
        }
    }

    private async Task SendInviteAsync(
        Organization organization,
        string providerAdminEmail)
    {
        var token = _dataProtector.Protect(
            $"BusinessUnitConversionInvite {organization.Id} {providerAdminEmail} {CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow)}");

        await mailService.SendBusinessUnitConversionInviteAsync(organization, token, providerAdminEmail);
    }

    private async Task<(Subscription, Provider, ProviderOrganization, ProviderUser)> ValidateFinalizationAsync(
        Organization organization,
        User? user,
        string token)
    {
        if (organization.PlanType.GetProductTier() != ProductTierType.Enterprise)
        {
            Fail("Organization must be on an enterprise plan.");
        }

        var subscription = await subscriberService.GetSubscription(organization);

        if (subscription is not
            {
                Status:
                StripeConstants.SubscriptionStatus.Active or
                StripeConstants.SubscriptionStatus.Trialing or
                StripeConstants.SubscriptionStatus.PastDue
            })
        {
            Fail("Organization must have a valid subscription.");
        }

        if (user == null)
        {
            Fail("Provider admin must be a Bitwarden user.");
        }

        if (!CoreHelpers.TokenIsValid(
                "BusinessUnitConversionInvite",
                _dataProtector,
                token,
                user.Email,
                organization.Id,
                globalSettings.OrganizationInviteExpirationHours))
        {
            Fail("Email token is invalid.");
        }

        var organizationUser =
            await organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id);

        if (organizationUser is not
            {
                Status: OrganizationUserStatusType.Confirmed
            })
        {
            Fail("Provider admin must be a confirmed member of the organization being converted.");
        }

        var provider = await providerRepository.GetByOrganizationIdAsync(organization.Id);

        if (provider is not
            {
                Type: ProviderType.BusinessUnit,
                Status: ProviderStatusType.Pending
            })
        {
            Fail("Linked provider is not a pending business unit.");
        }

        var providerUser = await providerUserRepository.GetByProviderUserAsync(provider.Id, user.Id);

        if (providerUser is not
            {
                Type: ProviderUserType.ProviderAdmin,
                Status: ProviderUserStatusType.Invited
            })
        {
            Fail("Provider admin has not been invited.");
        }

        var providerOrganization = await providerOrganizationRepository.GetByOrganizationId(organization.Id);

        return (subscription, provider, providerOrganization!, providerUser);

        [DoesNotReturn]
        void Fail(string scopedError)
        {
            logger.LogError("Could not finalize business unit conversion for organization ({OrganizationID}): {Error}",
                organization.Id, scopedError);
            throw new BillingException();
        }
    }

    private async Task<List<string>?> ValidateInitiationAsync(
        Organization organization,
        User? user)
    {
        var problems = new List<string>();

        if (organization.PlanType.GetProductTier() != ProductTierType.Enterprise)
        {
            problems.Add("Organization must be on an enterprise plan.");
        }

        var subscription = await subscriberService.GetSubscription(organization);

        if (subscription is not
            {
                Status:
                StripeConstants.SubscriptionStatus.Active or
                StripeConstants.SubscriptionStatus.Trialing or
                StripeConstants.SubscriptionStatus.PastDue
            })
        {
            problems.Add("Organization must have a valid subscription.");
        }

        var providerOrganization = await providerOrganizationRepository.GetByOrganizationId(organization.Id);

        if (providerOrganization != null)
        {
            problems.Add("Organization is already linked to a provider.");
        }

        if (user == null)
        {
            problems.Add("Provider admin must be a Bitwarden user.");
        }
        else
        {
            var organizationUser =
                await organizationUserRepository.GetByOrganizationAsync(organization.Id, user.Id);

            if (organizationUser is not
                {
                    Status: OrganizationUserStatusType.Confirmed
                })
            {
                problems.Add("Provider admin must be a confirmed member of the organization being converted.");
            }
        }

        return problems.Count == 0 ? null : problems;
    }

    #endregion
}
