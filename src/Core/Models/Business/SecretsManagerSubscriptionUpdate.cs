﻿using Bit.Core.AdminConsole.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Models.Business;

public class SecretsManagerSubscriptionUpdate
{
    public Organization Organization { get; }
    public Plan Plan { get; }

    /// <summary>
    /// The total seats the organization will have after the update, including any base seats included in the plan
    /// </summary>
    public int? SmSeats { get; set; }

    /// <summary>
    /// The new autoscale limit for seats after the update
    /// </summary>
    public int? MaxAutoscaleSmSeats { get; set; }

    /// <summary>
    /// The total service accounts the organization will have after the update, including the base service accounts
    /// included in the plan
    /// </summary>
    public int? SmServiceAccounts { get; set; }

    /// <summary>
    /// The new autoscale limit for service accounts after the update
    /// </summary>
    public int? MaxAutoscaleSmServiceAccounts { get; set; }

    /// <summary>
    /// Whether the subscription update is a result of autoscaling
    /// </summary>
    public bool Autoscaling { get; }

    /// <summary>
    /// The seats the organization will have after the update, excluding the base seats included in the plan
    /// Usually this is what the organization is billed for
    /// </summary>
    public int SmSeatsExcludingBase => SmSeats.HasValue ? SmSeats.Value - Plan.SecretsManager.BaseSeats : 0;
    /// <summary>
    /// The seats the organization will have after the update, excluding the base seats included in the plan
    /// Usually this is what the organization is billed for
    /// </summary>
    public int SmServiceAccountsExcludingBase => SmServiceAccounts.HasValue ? SmServiceAccounts.Value - Plan.SecretsManager!.BaseServiceAccount : 0;
    public bool SmSeatsChanged => SmSeats != Organization.SmSeats;
    public bool SmServiceAccountsChanged => SmServiceAccounts != Organization.SmServiceAccounts;
    public bool MaxAutoscaleSmSeatsChanged => MaxAutoscaleSmSeats != Organization.MaxAutoscaleSmSeats;
    public bool MaxAutoscaleSmServiceAccountsChanged =>
        MaxAutoscaleSmServiceAccounts != Organization.MaxAutoscaleSmServiceAccounts;
    public bool SmSeatAutoscaleLimitReached => SmSeats.HasValue && MaxAutoscaleSmSeats.HasValue && SmSeats == MaxAutoscaleSmSeats;

    public bool SmServiceAccountAutoscaleLimitReached => SmServiceAccounts.HasValue &&
                                                         MaxAutoscaleSmServiceAccounts.HasValue &&
                                                         SmServiceAccounts == MaxAutoscaleSmServiceAccounts;

    public SecretsManagerSubscriptionUpdate(Organization organization, Plan plan, bool autoscaling)
    {
        Organization = organization ?? throw new NotFoundException("Organization is not found.");
        Plan = plan;

        if (!Plan.SupportsSecretsManager)
        {
            throw new NotFoundException("Invalid Secrets Manager plan.");
        }

        SmSeats = organization.SmSeats;
        MaxAutoscaleSmSeats = organization.MaxAutoscaleSmSeats;
        SmServiceAccounts = organization.SmServiceAccounts;
        MaxAutoscaleSmServiceAccounts = organization.MaxAutoscaleSmServiceAccounts;
        Autoscaling = autoscaling;
    }

    public SecretsManagerSubscriptionUpdate AdjustSeats(int adjustment)
    {
        SmSeats = SmSeats.GetValueOrDefault() + adjustment;
        return this;
    }

    public SecretsManagerSubscriptionUpdate AdjustServiceAccounts(int adjustment)
    {
        SmServiceAccounts = SmServiceAccounts.GetValueOrDefault() + adjustment;
        return this;
    }
}
