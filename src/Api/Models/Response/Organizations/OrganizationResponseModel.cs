using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Models.Business;
using Bit.Core.Utilities;
using Constants = Bit.Core.Constants;

namespace Bit.Api.Models.Response.Organizations;

public class OrganizationResponseModel : ResponseModel
{
    public OrganizationResponseModel(Organization organization, string obj = "organization")
        : base(obj)
    {
        if (organization == null)
        {
            throw new ArgumentNullException(nameof(organization));
        }

        Id = organization.Id.ToString();
        Name = organization.Name;
        BusinessName = organization.BusinessName;
        BusinessAddress1 = organization.BusinessAddress1;
        BusinessAddress2 = organization.BusinessAddress2;
        BusinessAddress3 = organization.BusinessAddress3;
        BusinessCountry = organization.BusinessCountry;
        BusinessTaxNumber = organization.BusinessTaxNumber;
        BillingEmail = organization.BillingEmail;
        Plan = new PlanResponseModel(StaticStore.PasswordManagerPlans.FirstOrDefault(plan => plan.Type == organization.PlanType));
        SecretsManagerPlan = new PlanResponseModel(StaticStore.SecretManagerPlans.FirstOrDefault(plan => plan.Type == organization.PlanType));
        PlanType = organization.PlanType;
        Seats = organization.Seats;
        MaxAutoscaleSeats = organization.MaxAutoscaleSeats;
        MaxCollections = organization.MaxCollections;
        MaxStorageGb = organization.MaxStorageGb;
        UsePolicies = organization.UsePolicies;
        UseSso = organization.UseSso;
        UseKeyConnector = organization.UseKeyConnector;
        UseScim = organization.UseScim;
        UseGroups = organization.UseGroups;
        UseDirectory = organization.UseDirectory;
        UseEvents = organization.UseEvents;
        UseTotp = organization.UseTotp;
        Use2fa = organization.Use2fa;
        UseApi = organization.UseApi;
        UseResetPassword = organization.UseResetPassword;
        UseSecretsManager = organization.UseSecretsManager;
        UsersGetPremium = organization.UsersGetPremium;
        UseCustomPermissions = organization.UseCustomPermissions;
        SelfHost = organization.SelfHost;
        HasPublicAndPrivateKeys = organization.PublicKey != null && organization.PrivateKey != null;
        UsePasswordManager = organization.UsePasswordManager;
        SmSeats = organization.SmSeats;
        SmServiceAccounts = organization.SmServiceAccounts;
        MaxAutoscaleSmSeats = organization.MaxAutoscaleSmSeats;
        MaxAutoscaleSmServiceAccounts = organization.MaxAutoscaleSmServiceAccounts;
    }

    public string Id { get; set; }
    public string Name { get; set; }
    public string BusinessName { get; set; }
    public string BusinessAddress1 { get; set; }
    public string BusinessAddress2 { get; set; }
    public string BusinessAddress3 { get; set; }
    public string BusinessCountry { get; set; }
    public string BusinessTaxNumber { get; set; }
    public string BillingEmail { get; set; }
    public PlanResponseModel Plan { get; set; }
    public PlanResponseModel SecretsManagerPlan { get; set; }
    public PlanType PlanType { get; set; }
    public int? Seats { get; set; }
    public int? MaxAutoscaleSeats { get; set; } = null;
    public short? MaxCollections { get; set; }
    public short? MaxStorageGb { get; set; }
    public bool UsePolicies { get; set; }
    public bool UseSso { get; set; }
    public bool UseKeyConnector { get; set; }
    public bool UseScim { get; set; }
    public bool UseGroups { get; set; }
    public bool UseDirectory { get; set; }
    public bool UseEvents { get; set; }
    public bool UseTotp { get; set; }
    public bool Use2fa { get; set; }
    public bool UseApi { get; set; }
    public bool UseSecretsManager { get; set; }
    public bool UseResetPassword { get; set; }
    public bool UsersGetPremium { get; set; }
    public bool UseCustomPermissions { get; set; }
    public bool SelfHost { get; set; }
    public bool HasPublicAndPrivateKeys { get; set; }
    public bool UsePasswordManager { get; set; }
    public int? SmSeats { get; set; }
    public int? SmServiceAccounts { get; set; }
    public int? MaxAutoscaleSmSeats { get; set; }
    public int? MaxAutoscaleSmServiceAccounts { get; set; }
}

public class OrganizationSubscriptionResponseModel : OrganizationResponseModel
{
    public OrganizationSubscriptionResponseModel(Organization organization) : base(organization, "organizationSubscription")
    {
        Expiration = organization.ExpirationDate;
        StorageName = organization.Storage.HasValue ?
            CoreHelpers.ReadableBytesSize(organization.Storage.Value) : null;
        StorageGb = organization.Storage.HasValue ?
            Math.Round(organization.Storage.Value / 1073741824D, 2) : 0; // 1 GB
        SecretsManagerBeta = organization.SecretsManagerBeta;
    }

    public OrganizationSubscriptionResponseModel(Organization organization, SubscriptionInfo subscription, bool hideSensitiveData)
        : this(organization)
    {
        Subscription = subscription.Subscription != null ? new BillingSubscription(subscription.Subscription) : null;
        UpcomingInvoice = subscription.UpcomingInvoice != null ? new BillingSubscriptionUpcomingInvoice(subscription.UpcomingInvoice) : null;
        Expiration = DateTime.UtcNow.AddYears(1); // Not used, so just give it a value.

        if (hideSensitiveData)
        {
            BillingEmail = null;
            Subscription.Items = null;
            UpcomingInvoice.Amount = null;
        }

        SecretsManagerBeta = organization.SecretsManagerBeta;
    }

    public OrganizationSubscriptionResponseModel(Organization organization, OrganizationLicense license) :
        this(organization)
    {
        if (license != null)
        {
            // License expiration should always include grace period - See OrganizationLicense.cs
            Expiration = license.Expires;
            // Use license.ExpirationWithoutGracePeriod if available, otherwise assume license expiration minus grace period
            ExpirationWithoutGracePeriod = license.ExpirationWithoutGracePeriod ??
                                             license.Expires?.AddDays(-Constants
                                                 .OrganizationSelfHostSubscriptionGracePeriodDays);
        }

        SecretsManagerBeta = organization.SecretsManagerBeta;
    }

    public string StorageName { get; set; }
    public double? StorageGb { get; set; }
    public BillingSubscription Subscription { get; set; }
    public BillingSubscriptionUpcomingInvoice UpcomingInvoice { get; set; }

    /// <summary>
    /// Date when a self-hosted organization's subscription expires, without any grace period.
    /// </summary>
    public DateTime? ExpirationWithoutGracePeriod { get; set; }

    /// <summary>
    /// Date when a self-hosted organization expires (includes grace period).
    /// </summary>
    public DateTime? Expiration { get; set; }

    public bool SecretsManagerBeta { get; set; }
}
