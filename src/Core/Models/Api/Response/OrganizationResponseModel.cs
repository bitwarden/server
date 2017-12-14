using System;
using System.Linq;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using Bit.Core.Models.Business;

namespace Bit.Core.Models.Api
{
    public class OrganizationResponseModel : ResponseModel
    {
        public OrganizationResponseModel(Organization organization, string obj = "organization")
            : base(obj)
        {
            if(organization == null)
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
            Plan = organization.Plan;
            PlanType = organization.PlanType;
            Seats = organization.Seats;
            MaxCollections = organization.MaxCollections;
            UseGroups = organization.UseGroups;
            UseDirectory = organization.UseDirectory;
            UseEvents = organization.UseEvents;
            UseTotp = organization.UseTotp;
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
        public string Plan { get; set; }
        public Enums.PlanType PlanType { get; set; }
        public short? Seats { get; set; }
        public short? MaxCollections { get; set; }
        public bool UseGroups { get; set; }
        public bool UseDirectory { get; set; }
        public bool UseEvents { get; set; }
        public bool UseTotp { get; set; }
    }

    public class OrganizationBillingResponseModel : OrganizationResponseModel
    {
        public OrganizationBillingResponseModel(Organization organization, BillingInfo billing)
            : base(organization, "organizationBilling")
        {
            PaymentSource = billing.PaymentSource != null ? new BillingSource(billing.PaymentSource) : null;
            Subscription = billing.Subscription != null ? new BillingSubscription(billing.Subscription) : null;
            Charges = billing.Charges.Select(c => new BillingCharge(c));
            UpcomingInvoice = billing.UpcomingInvoice != null ? new BillingInvoice(billing.UpcomingInvoice) : null;
            StorageName = organization.Storage.HasValue ?
                Utilities.CoreHelpers.ReadableBytesSize(organization.Storage.Value) : null;
            StorageGb = organization.Storage.HasValue ? Math.Round(organization.Storage.Value / 1073741824D) : 0; // 1 GB
            MaxStorageGb = organization.MaxStorageGb;
            Expiration = DateTime.UtcNow.AddYears(1);
        }

        public OrganizationBillingResponseModel(Organization organization)
            : base(organization, "organizationBilling")
        {
            StorageName = organization.Storage.HasValue ?
                Utilities.CoreHelpers.ReadableBytesSize(organization.Storage.Value) : null;
            StorageGb = organization.Storage.HasValue ? Math.Round(organization.Storage.Value / 1073741824D, 2) : 0; // 1 GB
            MaxStorageGb = organization.MaxStorageGb;
            Expiration = organization.ExpirationDate;
        }

        public string StorageName { get; set; }
        public double? StorageGb { get; set; }
        public short? MaxStorageGb { get; set; }
        public BillingSource PaymentSource { get; set; }
        public BillingSubscription Subscription { get; set; }
        public BillingInvoice UpcomingInvoice { get; set; }
        public IEnumerable<BillingCharge> Charges { get; set; }
        public DateTime? Expiration { get; set; }
    }
}
