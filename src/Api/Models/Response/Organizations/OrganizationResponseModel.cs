using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Models.Business;
using Bit.Core.Utilities;

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
        Identifier = organization.Identifier;
        Name = organization.Name;
        BusinessName = organization.BusinessName;
        BusinessAddress1 = organization.BusinessAddress1;
        BusinessAddress2 = organization.BusinessAddress2;
        BusinessAddress3 = organization.BusinessAddress3;
        BusinessCountry = organization.BusinessCountry;
        BusinessTaxNumber = organization.BusinessTaxNumber;
        BillingEmail = organization.BillingEmail;
        Plan = new PlanResponseModel(StaticStore.Plans.FirstOrDefault(plan => plan.Type == organization.PlanType));
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
        UsersGetPremium = organization.UsersGetPremium;
        SelfHost = organization.SelfHost;
        HasPublicAndPrivateKeys = organization.PublicKey != null && organization.PrivateKey != null;
    }

    public string Id { get; set; }
    public string Identifier { get; set; }
    public string Name { get; set; }
    public string BusinessName { get; set; }
    public string BusinessAddress1 { get; set; }
    public string BusinessAddress2 { get; set; }
    public string BusinessAddress3 { get; set; }
    public string BusinessCountry { get; set; }
    public string BusinessTaxNumber { get; set; }
    public string BillingEmail { get; set; }
    public PlanResponseModel Plan { get; set; }
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
    public bool UseResetPassword { get; set; }
    public bool UsersGetPremium { get; set; }
    public bool SelfHost { get; set; }
    public bool HasPublicAndPrivateKeys { get; set; }
}

public class OrganizationSubscriptionResponseModel : OrganizationResponseModel
{
    public OrganizationSubscriptionResponseModel(Organization organization, SubscriptionInfo subscription = null)
        : base(organization, "organizationSubscription")
    {
        if (subscription != null)
        {
            Subscription = subscription.Subscription != null ?
                new BillingSubscription(subscription.Subscription) : null;
            UpcomingInvoice = subscription.UpcomingInvoice != null ?
                new BillingSubscriptionUpcomingInvoice(subscription.UpcomingInvoice) : null;
            Expiration = DateTime.UtcNow.AddYears(1); // Not used, so just give it a value.
        }
        else
        {
            Expiration = organization.ExpirationDate;
        }

        StorageName = organization.Storage.HasValue ?
            CoreHelpers.ReadableBytesSize(organization.Storage.Value) : null;
        StorageGb = organization.Storage.HasValue ?
            Math.Round(organization.Storage.Value / 1073741824D, 2) : 0; // 1 GB
    }

    public string StorageName { get; set; }
    public double? StorageGb { get; set; }
    public BillingSubscription Subscription { get; set; }
    public BillingSubscriptionUpcomingInvoice UpcomingInvoice { get; set; }
    public DateTime? Expiration { get; set; }
}
