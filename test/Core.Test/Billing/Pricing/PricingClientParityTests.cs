using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Exceptions;
using Bit.Core.Models.StaticStore;
using Bit.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;
using PremiumPlan = Bit.Core.Billing.Pricing.Premium.Plan;

namespace Bit.Core.Test.Billing.Pricing;

/// <summary>
/// Full field-level parity checks between <see cref="IPricingClient"/>'s local fallback and the live
/// remote Pricing Service. These are skipped by default — they make real HTTP calls to
/// <see cref="PricingServiceUri"/>, so they shouldn't run in CI. Unskip locally when you suspect the
/// local plan data has drifted from the remote service, run them, and use the failures as a checklist
/// to update the local data.
/// </summary>
public class PricingClientParityTests
{
    private const string ParitySkipReason =
        "Skipped by default. Unskip locally and run to detect drift between LocalPricingClient and the " +
        "live Pricing Service; use failures as a checklist to update the local plan data.";

    // QA Pricing Service — same URI used by the dev appsettings for SCIM/SSO. Change locally if you
    // need to point at a different environment.
    private const string PricingServiceUri = "https://billingpricing.qa.bitwarden.pw";

    [Fact(Skip = ParitySkipReason)]
    public async Task GetPlan_LocalMatchesRemote_ForEveryPlanType()
    {
        await using var localProvider = BuildPricingProvider(pricingUri: null);
        await using var remoteProvider = BuildPricingProvider(pricingUri: PricingServiceUri);
        var localClient = localProvider.GetRequiredService<IPricingClient>();
        var remoteClient = remoteProvider.GetRequiredService<IPricingClient>();

        var failures = new List<string>();

        foreach (var planType in Enum.GetValues<PlanType>())
        {
            var localPlan = await localClient.GetPlan(planType);
            var remotePlan = await remoteClient.GetPlan(planType);

            if (localPlan is null && remotePlan is null)
            {
                continue;
            }
            if (localPlan is null || remotePlan is null)
            {
                failures.Add($"{planType}: local={(localPlan is null ? "null" : "plan")}, remote={(remotePlan is null ? "null" : "plan")}");
                continue;
            }

            var fieldDiffs = DiffPlan(localPlan, remotePlan);
            if (fieldDiffs.Count > 0)
            {
                failures.Add($"{planType}: drift detected\n{string.Join("\n", fieldDiffs)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }

    [Fact(Skip = ParitySkipReason)]
    public async Task GetPlanOrThrow_LocalMatchesRemote_ForEveryPlanType()
    {
        await using var localProvider = BuildPricingProvider(pricingUri: null);
        await using var remoteProvider = BuildPricingProvider(pricingUri: PricingServiceUri);
        var localClient = localProvider.GetRequiredService<IPricingClient>();
        var remoteClient = remoteProvider.GetRequiredService<IPricingClient>();

        var failures = new List<string>();

        foreach (var planType in Enum.GetValues<PlanType>())
        {
            var localResult = await Capture(() => localClient.GetPlanOrThrow(planType));
            var remoteResult = await Capture(() => remoteClient.GetPlanOrThrow(planType));

            if (localResult.Threw != remoteResult.Threw)
            {
                failures.Add($"{planType}: local {(localResult.Threw ? "threw" : "returned")}, remote {(remoteResult.Threw ? "threw" : "returned")}");
                continue;
            }
            if (localResult.Threw)
            {
                continue;
            }

            var fieldDiffs = DiffPlan(localResult.Plan, remoteResult.Plan);
            if (fieldDiffs.Count > 0)
            {
                failures.Add($"{planType}: drift detected\n{string.Join("\n", fieldDiffs)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }

    [Fact(Skip = ParitySkipReason)]
    public async Task GetAvailablePremiumPlan_LocalMatchesRemote()
    {
        await using var localProvider = BuildPricingProvider(pricingUri: null);
        await using var remoteProvider = BuildPricingProvider(pricingUri: PricingServiceUri);
        var localClient = localProvider.GetRequiredService<IPricingClient>();
        var remoteClient = remoteProvider.GetRequiredService<IPricingClient>();

        var localPlan = await localClient.GetAvailablePremiumPlan();
        var remotePlan = await remoteClient.GetAvailablePremiumPlan();

        var fieldDiffs = DiffPremiumPlan(localPlan, remotePlan);
        Assert.True(fieldDiffs.Count == 0, $"drift detected\n{string.Join("\n", fieldDiffs)}");
    }

    private static async Task<(bool Threw, Plan? Plan)> Capture(Func<Task<Plan>> getPlan)
    {
        try
        {
            return (false, await getPlan());
        }
        catch (NotFoundException)
        {
            return (true, null);
        }
    }

    [Fact(Skip = ParitySkipReason)]
    public async Task ListPlans_LocalMatchesRemote_ForEveryField()
    {
        await using var localProvider = BuildPricingProvider(pricingUri: null);
        await using var remoteProvider = BuildPricingProvider(pricingUri: PricingServiceUri);
        var localClient = localProvider.GetRequiredService<IPricingClient>();
        var remoteClient = remoteProvider.GetRequiredService<IPricingClient>();

        var localPlans = await localClient.ListPlans();
        var remotePlans = await remoteClient.ListPlans();

        var remotePlansByType = remotePlans.ToDictionary(plan => plan.Type);
        var failures = new List<string>();

        foreach (var localPlan in localPlans)
        {
            if (!remotePlansByType.TryGetValue(localPlan.Type, out var remotePlan))
            {
                failures.Add($"{localPlan.Type}: present locally, absent remotely");
                continue;
            }

            var fieldDiffs = DiffPlan(localPlan, remotePlan);
            if (fieldDiffs.Count > 0)
            {
                failures.Add($"{localPlan.Type}: drift detected\n{string.Join("\n", fieldDiffs)}");
            }
        }

        foreach (var remotePlan in remotePlans)
        {
            if (localPlans.All(local => local.Type != remotePlan.Type))
            {
                failures.Add($"{remotePlan.Type}: present remotely, absent locally");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }

    [Fact(Skip = ParitySkipReason)]
    public async Task ListPremiumPlans_LocalMatchesRemote_ForEveryField()
    {
        await using var localProvider = BuildPricingProvider(pricingUri: null);
        await using var remoteProvider = BuildPricingProvider(pricingUri: PricingServiceUri);
        var localClient = localProvider.GetRequiredService<IPricingClient>();
        var remoteClient = remoteProvider.GetRequiredService<IPricingClient>();

        var localPlans = await localClient.ListPremiumPlans();
        var remotePlans = await remoteClient.ListPremiumPlans();

        var remotePlansByName = remotePlans.ToDictionary(plan => plan.Name);
        var failures = new List<string>();

        foreach (var localPlan in localPlans)
        {
            if (!remotePlansByName.TryGetValue(localPlan.Name, out var remotePlan))
            {
                failures.Add($"{localPlan.Name}: present locally, absent remotely");
                continue;
            }

            var fieldDiffs = DiffPremiumPlan(localPlan, remotePlan);
            if (fieldDiffs.Count > 0)
            {
                failures.Add($"{localPlan.Name}: drift detected\n{string.Join("\n", fieldDiffs)}");
            }
        }

        foreach (var remotePlan in remotePlans)
        {
            if (localPlans.All(local => local.Name != remotePlan.Name))
            {
                failures.Add($"{remotePlan.Name}: present remotely, absent locally");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }

    /// <summary>
    /// Per-field diff of every <see cref="Plan"/> property that the remote-side <c>PlanAdapter</c>
    /// populates. Two top-level fields are intentionally excluded: <c>CanBeUsedByBusiness</c> and
    /// <c>AutomaticUserConfirmation</c>. Neither is set by <c>PlanAdapter</c> (they're slated for
    /// removal per the TODOs in <see cref="Plan"/>), so comparing them would produce noise.
    /// </summary>
    private static List<string> DiffPlan(Plan local, Plan remote)
    {
        var diffs = new List<string>();
        AddIfDiffers(diffs, nameof(Plan.Type), local.Type, remote.Type);
        AddIfDiffers(diffs, nameof(Plan.ProductTier), local.ProductTier, remote.ProductTier);
        AddIfDiffers(diffs, nameof(Plan.Name), local.Name, remote.Name);
        AddIfDiffers(diffs, nameof(Plan.IsAnnual), local.IsAnnual, remote.IsAnnual);
        AddIfDiffers(diffs, nameof(Plan.NameLocalizationKey), local.NameLocalizationKey, remote.NameLocalizationKey);
        AddIfDiffers(diffs, nameof(Plan.DescriptionLocalizationKey), local.DescriptionLocalizationKey, remote.DescriptionLocalizationKey);
        AddIfDiffers(diffs, nameof(Plan.TrialPeriodDays), local.TrialPeriodDays, remote.TrialPeriodDays);
        AddIfDiffers(diffs, nameof(Plan.HasSelfHost), local.HasSelfHost, remote.HasSelfHost);
        AddIfDiffers(diffs, nameof(Plan.HasPolicies), local.HasPolicies, remote.HasPolicies);
        AddIfDiffers(diffs, nameof(Plan.HasGroups), local.HasGroups, remote.HasGroups);
        AddIfDiffers(diffs, nameof(Plan.HasDirectory), local.HasDirectory, remote.HasDirectory);
        AddIfDiffers(diffs, nameof(Plan.HasEvents), local.HasEvents, remote.HasEvents);
        AddIfDiffers(diffs, nameof(Plan.HasTotp), local.HasTotp, remote.HasTotp);
        AddIfDiffers(diffs, nameof(Plan.Has2fa), local.Has2fa, remote.Has2fa);
        AddIfDiffers(diffs, nameof(Plan.HasApi), local.HasApi, remote.HasApi);
        AddIfDiffers(diffs, nameof(Plan.HasSso), local.HasSso, remote.HasSso);
        AddIfDiffers(diffs, nameof(Plan.HasOrganizationDomains), local.HasOrganizationDomains, remote.HasOrganizationDomains);
        AddIfDiffers(diffs, nameof(Plan.HasKeyConnector), local.HasKeyConnector, remote.HasKeyConnector);
        AddIfDiffers(diffs, nameof(Plan.HasScim), local.HasScim, remote.HasScim);
        AddIfDiffers(diffs, nameof(Plan.HasResetPassword), local.HasResetPassword, remote.HasResetPassword);
        AddIfDiffers(diffs, nameof(Plan.UsersGetPremium), local.UsersGetPremium, remote.UsersGetPremium);
        AddIfDiffers(diffs, nameof(Plan.HasCustomPermissions), local.HasCustomPermissions, remote.HasCustomPermissions);
        AddIfDiffers(diffs, nameof(Plan.HasMyItems), local.HasMyItems, remote.HasMyItems);
        AddIfDiffers(diffs, nameof(Plan.HasInviteLinks), local.HasInviteLinks, remote.HasInviteLinks);
        AddIfDiffers(diffs, nameof(Plan.UpgradeSortOrder), local.UpgradeSortOrder, remote.UpgradeSortOrder);
        AddIfDiffers(diffs, nameof(Plan.DisplaySortOrder), local.DisplaySortOrder, remote.DisplaySortOrder);
        AddIfDiffers(diffs, nameof(Plan.LegacyYear), local.LegacyYear, remote.LegacyYear);
        AddIfDiffers(diffs, nameof(Plan.Disabled), local.Disabled, remote.Disabled);
        DiffPasswordManager(diffs, local.PasswordManager, remote.PasswordManager);
        DiffSecretsManager(diffs, local.SecretsManager, remote.SecretsManager);
        return diffs;
    }

    private static void DiffPasswordManager(List<string> diffs, Plan.PasswordManagerPlanFeatures local, Plan.PasswordManagerPlanFeatures remote)
    {
        const string prefix = nameof(Plan.PasswordManager);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.StripePlanId)}", local.StripePlanId, remote.StripePlanId);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.StripeSeatPlanId)}", local.StripeSeatPlanId, remote.StripeSeatPlanId);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.BasePrice)}", local.BasePrice, remote.BasePrice);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.SeatPrice)}", local.SeatPrice, remote.SeatPrice);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.ProviderPortalSeatPrice)}", local.ProviderPortalSeatPrice, remote.ProviderPortalSeatPrice);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.AllowSeatAutoscale)}", local.AllowSeatAutoscale, remote.AllowSeatAutoscale);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.HasAdditionalSeatsOption)}", local.HasAdditionalSeatsOption, remote.HasAdditionalSeatsOption);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.BaseSeats)}", local.BaseSeats, remote.BaseSeats);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.StripePremiumAccessPlanId)}", local.StripePremiumAccessPlanId, remote.StripePremiumAccessPlanId);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.PremiumAccessOptionPrice)}", local.PremiumAccessOptionPrice, remote.PremiumAccessOptionPrice);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.MaxSeats)}", local.MaxSeats, remote.MaxSeats);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.BaseStorageGb)}", local.BaseStorageGb, remote.BaseStorageGb);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.HasAdditionalStorageOption)}", local.HasAdditionalStorageOption, remote.HasAdditionalStorageOption);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.AdditionalStoragePricePerGb)}", local.AdditionalStoragePricePerGb, remote.AdditionalStoragePricePerGb);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.StripeStoragePlanId)}", local.StripeStoragePlanId, remote.StripeStoragePlanId);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.MaxCollections)}", local.MaxCollections, remote.MaxCollections);
        // HasPremiumAccessOption isn't set by PlanAdapter; it's TODO-removed alongside CanBeUsedByBusiness.
        // StripeProviderPortalSeatPlanId is obsolete (see [Obsolete] on Plan.PasswordManagerPlanFeatures); skip it.
    }

    private static void DiffSecretsManager(List<string> diffs, Plan.SecretsManagerPlanFeatures? local, Plan.SecretsManagerPlanFeatures? remote)
    {
        if (local is null && remote is null)
        {
            return;
        }
        if (local is null || remote is null)
        {
            diffs.Add($"  {nameof(Plan.SecretsManager)}: local={(local is null ? "null" : "set")}, remote={(remote is null ? "null" : "set")}");
            return;
        }

        const string prefix = nameof(Plan.SecretsManager);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.MaxServiceAccounts)}", local.MaxServiceAccounts, remote.MaxServiceAccounts);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.AllowServiceAccountsAutoscale)}", local.AllowServiceAccountsAutoscale, remote.AllowServiceAccountsAutoscale);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.StripeServiceAccountPlanId)}", local.StripeServiceAccountPlanId, remote.StripeServiceAccountPlanId);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.AdditionalPricePerServiceAccount)}", local.AdditionalPricePerServiceAccount, remote.AdditionalPricePerServiceAccount);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.BaseServiceAccount)}", local.BaseServiceAccount, remote.BaseServiceAccount);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.HasAdditionalServiceAccountOption)}", local.HasAdditionalServiceAccountOption, remote.HasAdditionalServiceAccountOption);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.StripeSeatPlanId)}", local.StripeSeatPlanId, remote.StripeSeatPlanId);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.HasAdditionalSeatsOption)}", local.HasAdditionalSeatsOption, remote.HasAdditionalSeatsOption);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.SeatPrice)}", local.SeatPrice, remote.SeatPrice);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.BaseSeats)}", local.BaseSeats, remote.BaseSeats);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.MaxSeats)}", local.MaxSeats, remote.MaxSeats);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.AllowSeatAutoscale)}", local.AllowSeatAutoscale, remote.AllowSeatAutoscale);
        AddIfDiffers(diffs, $"{prefix}.{nameof(local.MaxProjects)}", local.MaxProjects, remote.MaxProjects);
    }

    private static List<string> DiffPremiumPlan(PremiumPlan local, PremiumPlan remote)
    {
        var diffs = new List<string>();
        AddIfDiffers(diffs, nameof(PremiumPlan.Name), local.Name, remote.Name);
        AddIfDiffers(diffs, nameof(PremiumPlan.LegacyYear), local.LegacyYear, remote.LegacyYear);
        AddIfDiffers(diffs, nameof(PremiumPlan.Available), local.Available, remote.Available);
        AddIfDiffers(diffs, $"{nameof(PremiumPlan.Seat)}.{nameof(local.Seat.StripePriceId)}", local.Seat.StripePriceId, remote.Seat.StripePriceId);
        AddIfDiffers(diffs, $"{nameof(PremiumPlan.Seat)}.{nameof(local.Seat.Price)}", local.Seat.Price, remote.Seat.Price);
        AddIfDiffers(diffs, $"{nameof(PremiumPlan.Seat)}.{nameof(local.Seat.Provided)}", local.Seat.Provided, remote.Seat.Provided);
        AddIfDiffers(diffs, $"{nameof(PremiumPlan.Storage)}.{nameof(local.Storage.StripePriceId)}", local.Storage.StripePriceId, remote.Storage.StripePriceId);
        AddIfDiffers(diffs, $"{nameof(PremiumPlan.Storage)}.{nameof(local.Storage.Price)}", local.Storage.Price, remote.Storage.Price);
        AddIfDiffers(diffs, $"{nameof(PremiumPlan.Storage)}.{nameof(local.Storage.Provided)}", local.Storage.Provided, remote.Storage.Provided);
        return diffs;
    }

    private static void AddIfDiffers<T>(List<string> diffs, string fieldName, T local, T remote)
    {
        if (!Equals(local, remote))
        {
            diffs.Add($"  {fieldName}: local={Format(local)}, remote={Format(remote)}");
        }
    }

    private static string Format(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        _ => value.ToString() ?? "null",
    };

    /// <summary>
    /// Builds a DI container wired the same way the real services do — `AddPricingClient` selects
    /// the implementation based on <paramref name="pricingUri"/>: null/empty yields the local fallback,
    /// non-empty yields the HTTP-backed client pointed at that URI. Disposing the returned provider
    /// disposes the underlying <c>HttpClient</c>.
    /// </summary>
    private static ServiceProvider BuildPricingProvider(string? pricingUri)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new GlobalSettings { PricingUri = pricingUri, SelfHosted = false });

        var hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns(Environments.Development);
        services.AddSingleton(hostEnvironment);

        services.AddPricingClient();
        return services.BuildServiceProvider();
    }
}
