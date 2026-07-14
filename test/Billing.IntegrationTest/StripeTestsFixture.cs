using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Api.IntegrationTest.Factories;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Providers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Providers.Entities;
using Bit.Core.Billing.Providers.Repositories;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Billing.Subscriptions.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Test.Common.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Stripe;

namespace Bit.Billing.IntegrationTest;

public class StripeTestsFixture : IAsyncDisposable
{
    public ApiApplicationFactory Api { get; }
    public AdminApplicationFactory Admin { get; }

    public StripeTestsFixture()
    {
        Api = CreateApi();
        Admin = new AdminApplicationFactory(Api.TestDatabase);
    }

    /// <summary>
    /// Hook for subclasses (e.g. flag-override fixtures) to apply additional
    /// configuration to the API factory before the base fixture wires the
    /// Admin host against its TestDatabase.
    /// </summary>
    protected virtual ApiApplicationFactory CreateApi()
    {
        var api = new ApiApplicationFactory
        {
            StripeEnabled = true,
        };
        // Stripe test-mode caps at ~25 req/s per account. Our tests parallel-fire
        // real Stripe calls in bursts that exceed that on every full-suite run. The
        // Stripe SDK retries 429s that carry `Stripe-Should-Retry: true`, so simply
        // giving it more retries lets bursts naturally drain via the Retry-After
        // header. The production default of 2 isn't enough under test load;
        // MaxParallelThreads=2 in xunit.runner.json further reduces peak rate.
        api.UpdateConfiguration("globalSettings:stripe:maxNetworkRetries", "5");
        return api;
    }

    /// <summary>
    /// Builds a fresh <see cref="StripeClient"/> from the API host's resolved
    /// <see cref="GlobalSettings"/>. Avoids relying on a DI registration for
    /// <see cref="StripeClient"/>, which the production code does not provide.
    /// </summary>
    private StripeClient CreateStripeClient()
    {
        var settings = Api.Services.GetRequiredService<GlobalSettings>().Stripe;
        return new(settings.ApiKey, httpClient: new SystemNetHttpClient(maxNetworkRetries: settings.MaxNetworkRetries));
    }

    /// <summary>
    /// Registers a new user, logs them in, creates an organization on the
    /// requested plan billed via the Stripe test Visa card, and refreshes the
    /// access token so it carries the new organization-owner claims. Returns
    /// the authenticated client, the user and organization ids, and the latest
    /// refresh token for tests that need to re-issue (e.g. picking up further
    /// claims after a provider is created). Defaults to Enterprise (Annually)
    /// for the typical business-tier scenario.
    /// </summary>
    public async Task<(HttpClient Client, Guid UserId, Guid OrganizationId, string RefreshToken)>
        PrepareOrganizationOwnerAsync(string email, PlanType planType = PlanType.EnterpriseAnnually)
    {
        var (token, refreshToken) = await Api.LoginWithNewAccount(email);
        var client = Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var profileResponse = await client.GetAsync("/accounts/profile");
        await Assert.SuccessResponseAsync(profileResponse);
        var profile = (await profileResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        var userId = profile["id"]!.GetValue<Guid>();

        // Families is a fixed-seat plan; AdditionalSeats is rejected for it.
        // Business plans (Teams, Enterprise) take seats explicitly.
        var additionalSeats = planType is PlanType.FamiliesAnnually ? 0 : 10;

        var createResponse = await client.PostAsJsonAsync("/organizations", new
        {
            Name = "Test Organization",
            BusinessName = "Test Business Name",
            BillingEmail = email,
            PlanType = planType,
            Key = "test_key",
            Keys = new
            {
                PublicKey = "test_public_key",
                EncryptedPrivateKey = "test_encrypted_private_key",
            },
            PaymentToken = "pm_card_visa",
            PaymentMethodType = PaymentMethodType.Card,
            BillingAddressCountry = "US",
            BillingAddressPostalCode = "43432",
            AdditionalSeats = additionalSeats,
        });
        await Assert.SuccessResponseAsync(createResponse);

        var createdOrganization = (await createResponse.Content.ReadFromJsonAsync<JsonObject>())!;
        var organizationId = createdOrganization["id"]!.GetValue<Guid>();

        // Refresh so the bearer token carries the new organization-owner claims.
        (token, refreshToken) = await Api.Identity.TokenFromRefreshAsync(refreshToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return (client, userId, organizationId, refreshToken);
    }

    /// <summary>
    /// Registers a new user, creates an Enterprise organization billed via the
    /// Stripe test Visa card, runs an Admin-driven business-unit conversion to
    /// turn it into a Bitwarden provider, and refreshes the access token so it
    /// carries the new provider-admin claims. Returns the authenticated client
    /// and the new provider id.
    /// </summary>
    public async Task<(HttpClient Client, Guid ProviderId)> PrepareProviderAdminAsync(string email)
    {
        var (client, userId, organizationId, refreshToken) = await PrepareOrganizationOwnerAsync(email);

        var adminSession = await Admin.SignInAdminAsync();
        var invitationToken = await Admin.InitializeBusinessUnitConversionAsync(adminSession, organizationId, email);

        // Refresh to pick up organization-admin claims set during conversion init.
        var (token, providerRefreshToken) = await Api.Identity.TokenFromRefreshAsync(refreshToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var setupResponse = await client.PostAsJsonAsync(
            $"/organizations/{organizationId}/billing/setup-business-unit",
            new
            {
                UserId = userId,
                Token = invitationToken,
                ProviderKey = "provider_key",
                OrganizationKey = "organization_key",
            });
        await Assert.SuccessResponseAsync(setupResponse);

        var providerId = (await setupResponse.Content.ReadFromJsonAsync<JsonValue>())!.GetValue<Guid>();

        // Refresh again to pick up the new provider-admin claims.
        (token, _) = await Api.Identity.TokenFromRefreshAsync(providerRefreshToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return (client, providerId);
    }

    /// <summary>
    /// Creates a verified-instantly Stripe SetupIntent backed by a us_bank_account
    /// payment method (Stripe test routing + account numbers) and returns the
    /// payment method id. Tests pass this id to PUT /payment-method, where
    /// UpdatePaymentMethodCommand.AddBankAccountAsync lists setup intents with
    /// Expand=data.payment_method to attach it to the subscriber's customer.
    /// </summary>
    public async Task<string> CreateConfirmedBankAccountSetupIntentAsync(string email)
    {
        var stripeClient = CreateStripeClient();

        var paymentMethod = await stripeClient.V1.PaymentMethods.CreateAsync(new PaymentMethodCreateOptions
        {
            Type = "us_bank_account",
            UsBankAccount = new PaymentMethodUsBankAccountOptions
            {
                RoutingNumber = "110000000",
                AccountNumber = "000111111116",
                AccountHolderType = "individual",
                AccountType = "checking",
            },
            BillingDetails = new PaymentMethodBillingDetailsOptions
            {
                Name = "Test User",
                Email = email,
            },
        });

        await stripeClient.V1.SetupIntents.CreateAsync(new SetupIntentCreateOptions
        {
            PaymentMethod = paymentMethod.Id,
            PaymentMethodTypes = ["us_bank_account"],
            Usage = "off_session",
            Confirm = true,
            MandateData = new SetupIntentMandateDataOptions
            {
                CustomerAcceptance = new SetupIntentMandateDataCustomerAcceptanceOptions
                {
                    Type = "online",
                    Online = new SetupIntentMandateDataCustomerAcceptanceOnlineOptions
                    {
                        IpAddress = "127.0.0.1",
                        UserAgent = "Bit.Billing.IntegrationTest",
                    },
                },
            },
        });

        return paymentMethod.Id;
    }

    /// <summary>
    /// Registers a new user, purchases a Premium cloud-hosted subscription
    /// billed via the Stripe test Visa card, and refreshes the access token so
    /// it carries the new premium claim. Returns the authenticated client.
    /// </summary>
    public async Task<HttpClient> PreparePremiumUserAsync(string email)
    {
        var (token, refreshToken) = await Api.LoginWithNewAccount(email);
        var client = Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var subscriptionResponse = await client.PostAsJsonAsync("/account/billing/vnext/subscription", new
        {
            TokenizedPaymentMethod = new { Type = "card", Token = "pm_card_visa" },
            BillingAddress = new { Country = "US", PostalCode = "43432" },
            AdditionalStorageGb = 0,
        });
        await Assert.SuccessResponseAsync(subscriptionResponse);

        // Refresh so the bearer token carries the new premium claim.
        (token, _) = await Api.Identity.TokenFromRefreshAsync(refreshToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    /// <summary>
    /// Looks up the persisted Stripe customer id for an organization. Used by
    /// webhook tests to craft event payloads referencing real subscribers.
    /// </summary>
    public async Task<string> GetOrganizationGatewayCustomerIdAsync(Guid organizationId)
    {
        using var scope = Api.Services.CreateScope();
        var organization = await scope.ServiceProvider.GetRequiredService<IOrganizationRepository>()
            .GetByIdAsync(organizationId);
        return organization!.GatewayCustomerId!;
    }

    /// <summary>
    /// Looks up the persisted Stripe subscription id for an organization.
    /// </summary>
    public async Task<string> GetOrganizationGatewaySubscriptionIdAsync(Guid organizationId)
    {
        using var scope = Api.Services.CreateScope();
        var organization = await scope.ServiceProvider.GetRequiredService<IOrganizationRepository>()
            .GetByIdAsync(organizationId);
        return organization!.GatewaySubscriptionId!;
    }

    /// <summary>
    /// Resolves the Secrets Manager seat plan id (Stripe price/plan id) for a plan type,
    /// as SubscriptionUpdatedHandler does via <see cref="IPricingClient.ListPlans"/>. Used to
    /// synthesize a subscription.updated event whose previous_attributes carry an SM seat item.
    /// </summary>
    public async Task<string> GetSecretsManagerSeatPlanIdAsync(PlanType planType)
    {
        using var scope = Api.Services.CreateScope();
        var pricingClient = scope.ServiceProvider.GetRequiredService<IPricingClient>();
        var plan = await pricingClient.GetPlanOrThrow(planType);
        return plan.SecretsManager.StripeSeatPlanId;
    }

    /// <summary>
    /// Looks up the persisted Stripe customer id for a provider.
    /// </summary>
    public async Task<string> GetProviderGatewayCustomerIdAsync(Guid providerId)
    {
        using var scope = Api.Services.CreateScope();
        var provider = await scope.ServiceProvider.GetRequiredService<IProviderRepository>()
            .GetByIdAsync(providerId);
        return provider!.GatewayCustomerId!;
    }

    /// <summary>
    /// Looks up the persisted Stripe subscription id for a provider.
    /// </summary>
    public async Task<string> GetProviderGatewaySubscriptionIdAsync(Guid providerId)
    {
        using var scope = Api.Services.CreateScope();
        var provider = await scope.ServiceProvider.GetRequiredService<IProviderRepository>()
            .GetByIdAsync(providerId);
        return provider!.GatewaySubscriptionId!;
    }

    /// <summary>
    /// Returns the Stripe subscription's billing_mode.type (e.g. "classic"), used to verify the
    /// BillingMode the app sets at creation actually lands on the Stripe subscription.
    /// </summary>
    public async Task<string?> GetSubscriptionBillingModeTypeAsync(string subscriptionId)
    {
        var stripeClient = CreateStripeClient();
        var subscription = await stripeClient.V1.Subscriptions.GetAsync(subscriptionId);
        return subscription.BillingMode?.Type;
    }

    /// <summary>
    /// Ends the subscription's trial immediately so Stripe cuts a real subscription-cycle invoice
    /// (Parent.Type == "subscription_details") carrying any active subscription/customer discount,
    /// and returns that invoice's id. Avoids needing a test clock for a one-shot invoice.
    /// </summary>
    public async Task<string> EndTrialAndGetLatestInvoiceIdAsync(string subscriptionId)
    {
        var stripeClient = CreateStripeClient();
        var options = new SubscriptionUpdateOptions { ProrationBehavior = "none" };
        // The special value "now" ends the trial immediately; a timestamp would be rejected as "in the past".
        options.AddExtraParam("trial_end", "now");
        var updated = await stripeClient.V1.Subscriptions.UpdateAsync(subscriptionId, options);
        return updated.LatestInvoiceId;
    }

    /// <summary>
    /// Reads the ProviderInvoiceItem rows ProviderEventService persists for a given invoice.
    /// </summary>
    public async Task<ICollection<ProviderInvoiceItem>> GetProviderInvoiceItemsAsync(Guid providerId, string invoiceId)
    {
        using var scope = Api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IProviderInvoiceItemRepository>()
            .GetByProviderIdAndInvoiceId(providerId, invoiceId);
    }

    /// <summary>
    /// Resolves the ProviderPortalSeatPrice for a provider's first plan, used to compute the
    /// expected discounted line-item total.
    /// </summary>
    public async Task<decimal> GetProviderPortalSeatPriceAsync(Guid providerId)
    {
        using var scope = Api.Services.CreateScope();
        var providerPlans = await scope.ServiceProvider.GetRequiredService<IProviderPlanRepository>()
            .GetByProviderId(providerId);
        var pricingClient = scope.ServiceProvider.GetRequiredService<IPricingClient>();
        var plan = await pricingClient.GetPlanOrThrow(providerPlans.First().PlanType);
        return plan.PasswordManager.ProviderPortalSeatPrice;
    }

    /// <summary>
    /// Looks up the persisted Stripe customer id for a user (premium subscriber).
    /// </summary>
    public async Task<string> GetUserGatewayCustomerIdAsync(Guid userId)
    {
        using var scope = Api.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<IUserRepository>()
            .GetByIdAsync(userId);
        return user!.GatewayCustomerId!;
    }

    /// <summary>
    /// Convenience: same as <see cref="GetUserGatewayCustomerIdAsync(Guid)"/> but
    /// keyed by email, for premium prep helpers that don't hand back a user id.
    /// </summary>
    public async Task<string> GetUserGatewayCustomerIdByEmailAsync(string email)
    {
        using var scope = Api.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<IUserRepository>()
            .GetByEmailAsync(email);
        return user!.GatewayCustomerId!;
    }

    /// <summary>
    /// Looks up the persisted Stripe subscription id for a user (premium subscriber).
    /// </summary>
    public async Task<string> GetUserGatewaySubscriptionIdAsync(Guid userId)
    {
        using var scope = Api.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<IUserRepository>()
            .GetByIdAsync(userId);
        return user!.GatewaySubscriptionId!;
    }

    /// <summary>
    /// Same as <see cref="GetUserGatewaySubscriptionIdAsync(Guid)"/> but keyed by email, for
    /// premium prep helpers that don't hand back a user id.
    /// </summary>
    public async Task<string> GetUserGatewaySubscriptionIdByEmailAsync(string email)
    {
        using var scope = Api.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<IUserRepository>()
            .GetByEmailAsync(email);
        return user!.GatewaySubscriptionId!;
    }

    /// <summary>
    /// Reads the current metadata on a Stripe subscription — used to verify update/cancel flows
    /// don't clear keys (e.g. organizationId, userId) they don't explicitly set.
    /// </summary>
    public async Task<IDictionary<string, string>> GetSubscriptionMetadataAsync(string subscriptionId)
    {
        var stripeClient = CreateStripeClient();
        var subscription = await stripeClient.V1.Subscriptions.GetAsync(subscriptionId);
        return subscription.Metadata ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Returns whether the Stripe customer currently has an active discount (coupon expanded).
    /// </summary>
    public async Task<bool> CustomerHasDiscountAsync(string customerId)
    {
        var stripeClient = CreateStripeClient();
        var customer = await stripeClient.V1.Customers.GetAsync(customerId, new CustomerGetOptions
        {
            Expand = ["discount.source.coupon"],
        });
        return customer.Discount?.Source?.Coupon != null;
    }

    /// <summary>
    /// Creates a real test-mode charge against the organization's customer and
    /// returns its id, used by webhook tests that simulate <c>charge.succeeded</c>.
    /// Fresh signups are on a trial and have no charges yet, so we make one.
    /// </summary>
    public async Task<string> CreateChargeForOrganizationAsync(Guid organizationId)
    {
        var customerId = await GetOrganizationGatewayCustomerIdAsync(organizationId);
        var stripeClient = CreateStripeClient();
        var paymentIntent = await stripeClient.V1.PaymentIntents.CreateAsync(new PaymentIntentCreateOptions
        {
            Customer = customerId,
            Amount = 100,
            Currency = "usd",
            PaymentMethod = "pm_card_visa",
            Confirm = true,
            OffSession = true,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                AllowRedirects = "never",
            },
        });
        return paymentIntent.LatestChargeId!;
    }

    /// <summary>
    /// Creates an empty Stripe SetupIntent attached to the given customer, returning
    /// its id. Used by webhook tests that simulate <c>setup_intent.succeeded</c>.
    /// </summary>
    public async Task<string> CreateBareSetupIntentAsync(string customerId)
    {
        var stripeClient = CreateStripeClient();
        var setupIntent = await stripeClient.V1.SetupIntents.CreateAsync(new SetupIntentCreateOptions
        {
            Customer = customerId,
            PaymentMethodTypes = ["card"],
            Usage = "off_session",
        });
        return setupIntent.Id;
    }

    /// <summary>
    /// Drives <see cref="ISubscriberService.ScheduleUnpaidCancellationAsync"/> directly
    /// against an organization. This is what the Admin Portal's POST /organizations/{id}
    /// "Edit" form does when an admin disables a billing-disabled org. The Stripe fetch
    /// inside that method runs regardless of subscription status.
    /// </summary>
    public async Task ScheduleUnpaidCancellationForOrganizationAsync(Guid organizationId)
    {
        using var scope = Api.Services.CreateScope();
        var organization = await scope.ServiceProvider.GetRequiredService<IOrganizationRepository>()
            .GetByIdAsync(organizationId);
        await scope.ServiceProvider.GetRequiredService<ISubscriberService>()
            .ScheduleUnpaidCancellationAsync(organization!);
    }

    /// <summary>
    /// Drives <see cref="ISubscriberService.ResumeFromUnpaidCancellationAsync"/> directly
    /// against an organization. The Stripe fetch inside that method runs regardless of
    /// subscription status.
    /// </summary>
    public async Task ResumeFromUnpaidCancellationForOrganizationAsync(Guid organizationId)
    {
        using var scope = Api.Services.CreateScope();
        var organization = await scope.ServiceProvider.GetRequiredService<IOrganizationRepository>()
            .GetByIdAsync(organizationId);
        await scope.ServiceProvider.GetRequiredService<ISubscriberService>()
            .ResumeFromUnpaidCancellationAsync(organization!);
    }

    /// <summary>
    /// Cancels the user's subscription immediately (not at period end), forcing the user
    /// off premium and leaving a Canceled subscription on the existing Stripe customer.
    /// Used by the "existing customer re-subscribes" branch test.
    /// </summary>
    public async Task CancelUserSubscriptionImmediatelyAsync(Guid userId)
    {
        using var scope = Api.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAsync(userId);
        await scope.ServiceProvider.GetRequiredService<ISubscriberService>()
            .CancelSubscription(user!, cancelImmediately: true);

        // CancelSubscription updates Stripe state; reflect "no longer premium" on the
        // user row so the re-subscribe controller path doesn't reject with "Already a
        // premium user."
        user!.Premium = false;
        user.PremiumExpirationDate = null;
        await scope.ServiceProvider.GetRequiredService<IUserRepository>().ReplaceAsync(user);
    }

    /// <summary>
    /// Creates a bare Stripe customer attached to the user (no subscription, no payment
    /// method) and persists its id on the user row. Drives the
    /// "user has GatewayCustomerId but no premium" code paths in premium creation and
    /// in the UserHasNoPreviousSubscriptions discount filter.
    /// </summary>
    public async Task CreateOrphanedStripeCustomerForUserAsync(Guid userId, string email)
    {
        var stripeClient = CreateStripeClient();
        var customer = await stripeClient.V1.Customers.CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Description = $"Integration test orphaned customer for {userId}",
        });

        using var scope = Api.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await repo.GetByIdAsync(userId);
        user!.Gateway = GatewayType.Stripe;
        user.GatewayCustomerId = customer.Id;
        await repo.ReplaceAsync(user);
    }

    /// <summary>
    /// Seeds an active <see cref="SubscriptionDiscount"/> with audience
    /// <see cref="DiscountAudienceType.UserHasNoPreviousSubscriptions"/> applicable to the
    /// Premium product, plus a real Stripe coupon backing it. Returns the coupon id.
    /// </summary>
    public async Task<string> SeedNoPreviousSubscriptionsDiscountAsync(string couponId)
    {
        var stripeClient = CreateStripeClient();

        try { await stripeClient.V1.Coupons.DeleteAsync(couponId); }
        catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing") { /* coupon doesn't exist yet — first run */ }

        await stripeClient.V1.Coupons.CreateAsync(new CouponCreateOptions
        {
            Id = couponId,
            Name = "Integration Test New-User Discount",
            PercentOff = 10,
            Duration = "once",
        });

        using var scope = Api.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISubscriptionDiscountRepository>();
        await repo.CreateAsync(new SubscriptionDiscount
        {
            StripeCouponId = couponId,
            StripeProductIds = [StripeConstants.ProductIDs.Premium],
            PercentOff = 10,
            Duration = "once",
            Name = "New-User Premium Discount",
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(30),
            AudienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions,
        });
        return couponId;
    }

    /// <summary>
    /// Drives <see cref="IRemoveOrganizationFromProviderCommand"/> directly. Exercises
    /// <see cref="ISubscriberService.RemovePaymentSource"/> on the org as part of
    /// unlinking it from a provider.
    /// </summary>
    /// <summary>
    /// Adopts an existing (Stripe-enabled, non-Managed) organization into a newly-created reseller
    /// provider via a direct ProviderOrganization link — the org keeps its own subscription and its
    /// Created status. This is the topology that routes <see cref="RemoveAnyOrganizationFromProviderAsync"/>
    /// to the `else if (organization.IsStripeEnabled())` branch (discount deletion), unlike
    /// business-unit conversion which produces a Managed org with no own subscription.
    /// </summary>
    public async Task<Guid> AdoptOrganizationIntoResellerProviderAsync(Guid organizationId)
    {
        using var scope = Api.Services.CreateScope();
        var providerRepo = scope.ServiceProvider.GetRequiredService<IProviderRepository>();
        var providerOrgRepo = scope.ServiceProvider.GetRequiredService<IProviderOrganizationRepository>();

        var provider = await providerRepo.CreateAsync(new Provider
        {
            Name = $"reseller-{Guid.NewGuid():N}",
            Type = ProviderType.Reseller,
            Status = ProviderStatusType.Created,
            Enabled = true,
            UseEvents = false,
            BillingEmail = "reseller@example.com",
        });

        await providerOrgRepo.CreateAsync(new ProviderOrganization
        {
            ProviderId = provider.Id,
            OrganizationId = organizationId,
        });

        return provider.Id;
    }

    public async Task RemoveAnyOrganizationFromProviderAsync(Guid providerId)
    {
        using var scope = Api.Services.CreateScope();
        var providerRepo = scope.ServiceProvider.GetRequiredService<IProviderRepository>();
        var providerOrgRepo = scope.ServiceProvider.GetRequiredService<IProviderOrganizationRepository>();
        var orgRepo = scope.ServiceProvider.GetRequiredService<IOrganizationRepository>();
        var command = scope.ServiceProvider.GetRequiredService<IRemoveOrganizationFromProviderCommand>();

        var provider = await providerRepo.GetByIdAsync(providerId);
        var providerOrgDetails = (await providerOrgRepo.GetManyDetailsByProviderAsync(providerId)).First();
        var providerOrg = await providerOrgRepo.GetByIdAsync(providerOrgDetails.Id);
        var organization = await orgRepo.GetByIdAsync(providerOrgDetails.OrganizationId);

        await command.RemoveOrganizationFromProvider(provider!, providerOrg!, organization!);
    }

    /// <summary>
    /// Detaches the customer's default Stripe payment method. Drives the
    /// no-default-PM branch inside <c>GetPaymentSourceAsync</c> which lists setup intents
    /// with Expand=["data.payment_method"].
    /// </summary>
    public async Task DetachDefaultPaymentMethodAsync(string customerId)
    {
        var stripeClient = CreateStripeClient();
        var customer = await stripeClient.V1.Customers.GetAsync(customerId);
        if (!string.IsNullOrEmpty(customer.InvoiceSettings.DefaultPaymentMethodId))
        {
            await stripeClient.V1.PaymentMethods.DetachAsync(customer.InvoiceSettings.DefaultPaymentMethodId);
        }
    }

    /// <summary>
    /// Creates a Stripe coupon (deletes any pre-existing one first), inserts a churn-only
    /// migration cohort that references it, and assigns the organization to that cohort.
    /// Drives the <see cref="GetChurnMitigationOfferQuery"/> and
    /// <see cref="RedeemChurnMitigationOfferCommand"/> Expand-using fetches.
    /// </summary>
    public async Task<string> SeedChurnOnlyCohortAsync(Guid organizationId, string couponId)
    {
        var stripeClient = CreateStripeClient();

        try { await stripeClient.V1.Coupons.DeleteAsync(couponId); }
        catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing") { /* coupon doesn't exist yet — first run */ }

        await stripeClient.V1.Coupons.CreateAsync(new CouponCreateOptions
        {
            Id = couponId,
            Name = "Integration Test Churn Coupon",
            PercentOff = 25,
            Duration = "repeating",
            DurationInMonths = 3,
        });

        using var scope = Api.Services.CreateScope();
        var cohortRepository = scope.ServiceProvider
            .GetRequiredService<IOrganizationPlanMigrationCohortRepository>();
        var cohort = await cohortRepository.CreateAsync(new OrganizationPlanMigrationCohort
        {
            Name = $"churn-only-{Guid.NewGuid():N}",
            MigrationPathId = null,
            ChurnDiscountCouponCode = couponId,
            IsActive = true,
        });

        var assignmentRepository = scope.ServiceProvider
            .GetRequiredService<IOrganizationPlanMigrationCohortAssignmentRepository>();
        await assignmentRepository.CreateAsync(new OrganizationPlanMigrationCohortAssignment
        {
            OrganizationId = organizationId,
            CohortId = cohort.Id,
        });

        return couponId;
    }

    /// <summary>
    /// Seeds a migration-cohort assignment and creates an active Stripe SubscriptionSchedule
    /// on the organization's subscription so the <see cref="GetBitwardenSubscriptionQuery"/>
    /// and <see cref="StripePaymentService"/> schedule-Expand sites are reachable.
    /// </summary>
    public async Task SeedMigrationCohortWithScheduleAsync(Guid organizationId, MigrationPathId migrationPathId)
    {
        var stripeClient = CreateStripeClient();
        var subscriptionId = await GetOrganizationGatewaySubscriptionIdAsync(organizationId);

        await stripeClient.V1.SubscriptionSchedules.CreateAsync(new SubscriptionScheduleCreateOptions
        {
            FromSubscription = subscriptionId,
        });

        using var scope = Api.Services.CreateScope();
        var cohortRepository = scope.ServiceProvider
            .GetRequiredService<IOrganizationPlanMigrationCohortRepository>();
        var cohort = await cohortRepository.CreateAsync(new OrganizationPlanMigrationCohort
        {
            Name = $"migration-{Guid.NewGuid():N}",
            MigrationPathId = migrationPathId,
            IsActive = true,
        });

        var assignmentRepository = scope.ServiceProvider
            .GetRequiredService<IOrganizationPlanMigrationCohortAssignmentRepository>();
        await assignmentRepository.CreateAsync(new OrganizationPlanMigrationCohortAssignment
        {
            OrganizationId = organizationId,
            CohortId = cohort.Id,
        });
    }

    /// <summary>
    /// Creates a Stripe coupon whose <c>applies_to.products</c> scopes it to the
    /// Premium product, then re-fetches it with an <em>empty</em> expand list and
    /// returns the retrieved <see cref="CouponAppliesTo"/>. Any pre-existing coupon
    /// with the same id is deleted first so the test is re-runnable.
    ///
    /// Isolates the load-bearing invariant behind
    /// <see cref="GetBitwardenSubscriptionQuery"/> and
    /// <see cref="StripePaymentService"/> dropping <c>.applies_to</c> from
    /// <c>customer.discount.source.coupon.applies_to</c> (and
    /// <c>phases.discounts.source.coupon.applies_to</c>) expand paths that would
    /// otherwise exceed Stripe's 4-level cap: <c>Coupon.applies_to</c> arrives
    /// inline on the Coupon JSON regardless of whether the caller expanded it.
    /// If Stripe changes that contract, this returns a null / empty products list
    /// and every downstream product-vs-cart discount classification silently flips.
    /// </summary>
    public async Task<CouponAppliesTo?> CreateAndReloadProductScopedCouponAsync(string couponId)
    {
        var stripeClient = CreateStripeClient();

        try { await stripeClient.V1.Coupons.DeleteAsync(couponId); }
        catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing") { /* first run */ }

        await stripeClient.V1.Coupons.CreateAsync(new CouponCreateOptions
        {
            Id = couponId,
            Name = "Integration Test Product-Scoped Coupon",
            PercentOff = 10,
            Duration = "once",
            AppliesTo = new CouponAppliesToOptions
            {
                Products = [StripeConstants.ProductIDs.Premium],
            },
        });

        // No Expand at all — the whole point is to prove applies_to is inline.
        // (Passing an empty array serializes as `expand=` on the wire, which
        // Stripe rejects as an attempt to unset the parameter.)
        var reloaded = await stripeClient.V1.Coupons.GetAsync(couponId);

        return reloaded.AppliesTo;
    }

    /// <summary>
    /// Companion to <see cref="CreateAndReloadProductScopedCouponAsync"/> that fetches
    /// the same coupon with <c>Expand = ["applies_to"]</c>. Used to confirm that when
    /// the direct-probe test finds <c>AppliesTo</c> null without expansion, the same
    /// coupon does carry the products list when the caller explicitly expands
    /// <c>applies_to</c> — pinning the empirical rule.
    /// </summary>
    public async Task<CouponAppliesTo?> ReloadCouponWithAppliesToExpandedAsync(string couponId)
    {
        var stripeClient = CreateStripeClient();
        var reloaded = await stripeClient.V1.Coupons.GetAsync(couponId, new CouponGetOptions
        {
            Expand = ["applies_to"],
        });
        return reloaded.AppliesTo;
    }

    /// <summary>
    /// Purchases premium and attaches a product-scoped Stripe coupon (bound to the
    /// Premium product via <c>applies_to.products</c>) to the resulting subscription's
    /// discount list. Any pre-existing coupon with the same id is deleted first so
    /// the test is re-runnable.
    ///
    /// Exercises <see cref="GetBitwardenSubscriptionQuery.GetRelevantCouponsAsync"/>'s
    /// per-coupon enrichment step end-to-end: the parent subscription fetch stops at
    /// <c>discounts.source.coupon.applies_to</c>, so the seat-scoped coupon should
    /// land on <c>cart.passwordManager.seats.discount</c> (product-level) rather than
    /// <c>cart.discount</c> (cart-level).
    /// </summary>
    public async Task<HttpClient> PreparePremiumUserWithProductScopedSubscriptionCouponAsync(
        string email, string couponId)
    {
        var stripeClient = CreateStripeClient();

        try { await stripeClient.V1.Coupons.DeleteAsync(couponId); }
        catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing") { /* first run */ }

        await stripeClient.V1.Coupons.CreateAsync(new CouponCreateOptions
        {
            Id = couponId,
            Name = "IT Product-Scoped Premium Coupon",
            PercentOff = 10,
            Duration = "once",
            AppliesTo = new CouponAppliesToOptions
            {
                Products = [StripeConstants.ProductIDs.Premium],
            },
        });

        var client = await PreparePremiumUserAsync(email);

        string subscriptionId;
        using (var scope = Api.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<IUserRepository>()
                .GetByEmailAsync(email);
            subscriptionId = user!.GatewaySubscriptionId!;
        }

        await stripeClient.V1.Subscriptions.UpdateAsync(subscriptionId,
            new SubscriptionUpdateOptions
            {
                Discounts = [new SubscriptionDiscountOptions { Coupon = couponId }],
            });

        return client;
    }

    /// <summary>
    /// Purchases premium, wraps the resulting Stripe subscription in a
    /// <see cref="SubscriptionSchedule"/>, and appends a future Phase 2 whose
    /// Discounts reference a product-scoped Stripe coupon (bound to the Premium
    /// product via <c>applies_to.products</c>). Any pre-existing coupon with the
    /// same id is deleted first so the test is re-runnable.
    ///
    /// Exercises the schedule Phase-2 read path in
    /// <see cref="GetBitwardenSubscriptionQuery"/> — the <c>phases.discounts.coupon.applies_to</c>
    /// expand, plus the <c>d.Coupon</c> reader that <see cref="SubscriptionSchedulePhaseDiscount"/>
    /// actually exposes (as opposed to the wire-invalid <c>d.Discount.Source.Coupon</c>
    /// path the SDK bump initially introduced).
    /// </summary>
    public async Task<HttpClient> PreparePremiumUserWithProductScopedPhase2CouponAsync(
        string email, string couponId)
    {
        var stripeClient = CreateStripeClient();

        try { await stripeClient.V1.Coupons.DeleteAsync(couponId); }
        catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing") { /* first run */ }

        await stripeClient.V1.Coupons.CreateAsync(new CouponCreateOptions
        {
            Id = couponId,
            Name = "IT Phase2 Premium Coupon",
            PercentOff = 10,
            Duration = "once",
            AppliesTo = new CouponAppliesToOptions
            {
                Products = [StripeConstants.ProductIDs.Premium],
            },
        });

        var client = await PreparePremiumUserAsync(email);

        string subscriptionId;
        using (var scope = Api.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<IUserRepository>()
                .GetByEmailAsync(email);
            subscriptionId = user!.GatewaySubscriptionId!;
        }

        // Wrap the existing subscription in a schedule so `subscription.ScheduleId`
        // is populated on the GET path.
        var schedule = await stripeClient.V1.SubscriptionSchedules.CreateAsync(new SubscriptionScheduleCreateOptions
        {
            FromSubscription = subscriptionId,
        });

        // Append a future Phase 2 that carries the product-scoped coupon.
        // FromSubscription seeds Phase 1 from the current sub; we mirror its items
        // and add a follow-up phase with a matching item and the new coupon.
        var phase1 = schedule.Phases[0];
        var phase1EndDate = phase1.EndDate;
        var phase1Items = phase1.Items
            .Select(i => new SubscriptionSchedulePhaseItemOptions
            {
                Price = i.PriceId,
                Quantity = i.Quantity,
            })
            .ToList();

        await stripeClient.V1.SubscriptionSchedules.UpdateAsync(schedule.Id,
            new SubscriptionScheduleUpdateOptions
            {
                Phases =
                [
                    new SubscriptionSchedulePhaseOptions
                    {
                        StartDate = phase1.StartDate,
                        EndDate = phase1EndDate,
                        Items = phase1Items,
                    },
                    new SubscriptionSchedulePhaseOptions
                    {
                        StartDate = phase1EndDate,
                        EndDate = phase1EndDate.AddYears(1),
                        Items = phase1Items,
                        Discounts = [new SubscriptionSchedulePhaseDiscountOptions { Coupon = couponId }],
                    },
                ],
            });

        return client;
    }

    /// <summary>
    /// Creates (or replaces) a Stripe coupon with the given id. Optionally scoped
    /// to a product via <c>applies_to.products</c>. Any pre-existing coupon with
    /// the same id is deleted first so the test is re-runnable.
    /// </summary>
    public async Task CreateStripeCouponAsync(
        string couponId,
        decimal percentOff,
        string? scopedToProductId = null)
    {
        var stripeClient = CreateStripeClient();

        try { await stripeClient.V1.Coupons.DeleteAsync(couponId); }
        catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing") { /* first run */ }

        await stripeClient.V1.Coupons.CreateAsync(new CouponCreateOptions
        {
            Id = couponId,
            Name = "IT Customer Coupon",
            PercentOff = percentOff,
            Duration = "once",
            AppliesTo = scopedToProductId is not null
                ? new CouponAppliesToOptions { Products = [scopedToProductId] }
                : null,
        });
    }

    /// <summary>
    /// Attaches a coupon to a Stripe customer via a raw <c>POST /v1/customers/{id}</c>
    /// with a legacy <c>Stripe-Version</c> header. Stripe.net 51.1 (pinned at
    /// <c>2026-04-22.dahlia</c>) removed <c>coupon</c> from the typed
    /// <see cref="CustomerUpdateOptions"/>, and the newer HTTP endpoint rejects
    /// <c>coupon</c> in the body. Since Bitwarden never programmatically writes
    /// <see cref="Stripe.Customer.Discount"/> in production (see the comment in
    /// <see cref="Bit.Core.Billing.Organizations.PlanMigration.Queries.GetChurnMitigationOfferQuery"/>
    /// about manual coupon application by finance), we simulate that state with
    /// a request pinned to a pre-clover API version. The subsequent SDK reads
    /// use the current API version and return the discount in the modern
    /// <c>Discount.Source.Coupon</c> shape.
    /// </summary>
    private async Task AttachCustomerCouponAsync(string customerId, string couponId)
    {
        var settings = Api.Services.GetRequiredService<GlobalSettings>().Stripe;
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        // Pre-clover API version: still accepts the `coupon` body param on customer update.
        http.DefaultRequestHeaders.Add("Stripe-Version", "2024-06-20");

        using var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("coupon", couponId),
        });

        var response = await http.PostAsync(
            $"https://api.stripe.com/v1/customers/{customerId}", content);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Convenience: seed a percent-off coupon and attach it to the given customer.
    /// Callers pass the customer id resolved from an already-prepared org/premium/provider.
    /// Verifies the attach took effect by re-fetching the customer via the typed
    /// SDK and asserting <see cref="Stripe.Customer.Discount"/> is populated —
    /// guards against the raw-HTTP write silently no-op'ing if Stripe ever stops
    /// honoring the legacy <c>Stripe-Version</c> header for this endpoint.
    /// </summary>
    public async Task SeedAndAttachCustomerCouponAsync(
        string customerId,
        string couponId,
        decimal percentOff,
        string? scopedToProductId = null)
    {
        await CreateStripeCouponAsync(couponId, percentOff, scopedToProductId);
        await AttachCustomerCouponAsync(customerId, couponId);

        var stripeClient = CreateStripeClient();
        var customer = await stripeClient.V1.Customers.GetAsync(customerId, new CustomerGetOptions
        {
            Expand = ["discount.source.coupon"],
        });

        if (customer.Discount?.Source?.Coupon?.Id != couponId)
        {
            throw new InvalidOperationException(
                $"Raw-HTTP customer-coupon attach did not take effect for customer {customerId}. " +
                $"Expected Discount.Source.Coupon.Id = '{couponId}', got '{customer.Discount?.Source?.Coupon?.Id ?? "<null>"}'. " +
                $"Stripe may have stopped accepting the legacy Stripe-Version pinned in AttachCustomerCouponAsync.");
        }
    }

    /// <summary>
    /// Fetches the org's Stripe subscription and returns the first line item's
    /// product id. Used by tests that need to scope a coupon's
    /// <c>applies_to.products</c> to a product actually present on the subscription
    /// (e.g. the SM standalone metadata check's product-id intersection).
    /// </summary>
    public async Task<string> GetOrganizationFirstProductIdAsync(Guid organizationId)
    {
        var stripeClient = CreateStripeClient();
        var subscriptionId = await GetOrganizationGatewaySubscriptionIdAsync(organizationId);
        var subscription = await stripeClient.V1.Subscriptions.GetAsync(subscriptionId, new SubscriptionGetOptions
        {
            Expand = ["items.data.price.product"],
        });
        return subscription.Items.Data.First().Price.Product.Id;
    }

    /// <summary>
    /// Reads back the subscription's current sub-level discount coupon ids. Used
    /// by redeem tests that need to verify the merged set on Stripe after the
    /// call — e.g. asserting the customer coupon survived the merge into the
    /// subscription's Discounts list.
    /// </summary>
    public async Task<List<string>> GetSubscriptionDiscountCouponIdsAsync(string subscriptionId)
    {
        var stripeClient = CreateStripeClient();
        var subscription = await stripeClient.V1.Subscriptions.GetAsync(subscriptionId, new SubscriptionGetOptions
        {
            Expand = ["discounts.source.coupon"],
        });
        return subscription.Discounts?
            .Where(d => d?.Source?.Coupon?.Id is not null)
            .Select(d => d.Source.Coupon.Id)
            .ToList() ?? [];
    }

    /// <summary>
    /// Attaches a product-scoped Stripe coupon to a subscription's Discounts list
    /// via the typed SDK (no raw-HTTP legacy header needed for the subscription
    /// endpoint — <c>SubscriptionUpdateOptions.Discounts</c> is still typed).
    /// Deletes any pre-existing coupon with the same id first for re-runnability.
    /// </summary>
    public async Task SeedAndAttachSubscriptionCouponAsync(
        string subscriptionId,
        string couponId,
        decimal percentOff,
        string? scopedToProductId = null)
    {
        await CreateStripeCouponAsync(couponId, percentOff, scopedToProductId);

        var stripeClient = CreateStripeClient();
        await stripeClient.V1.Subscriptions.UpdateAsync(subscriptionId, new SubscriptionUpdateOptions
        {
            Discounts = [new SubscriptionDiscountOptions { Coupon = couponId }],
        });
    }

    /// <summary>
    /// Creates a no-op Stripe Checkout Session attached to the given customer for
    /// webhook tests that simulate <c>checkout.session.completed</c>.
    /// </summary>
    public async Task<string> CreateCheckoutSessionAsync(string customerId)
    {
        var stripeClient = CreateStripeClient();
        var session = await stripeClient.V1.Checkout.Sessions.CreateAsync(new Stripe.Checkout.SessionCreateOptions
        {
            Customer = customerId,
            Mode = "setup",
            PaymentMethodTypes = ["card"],
            SuccessUrl = "https://example.com/success",
            CancelUrl = "https://example.com/cancel",
        });
        return session.Id;
    }

    public virtual async ValueTask DisposeAsync()
    {
        await Admin.DisposeAsync();
        await Api.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
