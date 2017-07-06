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
            BillingEmail = organization.BillingEmail;
            Plan = organization.Plan;
            PlanType = organization.PlanType;
            Seats = organization.Seats;
            MaxCollections = organization.MaxCollections;
            UseGroups = organization.UseGroups;
            UseDirectory = organization.UseDirectory;
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string BusinessName { get; set; }
        public string BillingEmail { get; set; }
        public string Plan { get; set; }
        public Enums.PlanType PlanType { get; set; }
        public short? Seats { get; set; }
        public short? MaxCollections { get; set; }
        public bool UseGroups { get; set; }
        public bool UseDirectory { get; set; }
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
        }

        public BillingSource PaymentSource { get; set; }
        public BillingSubscription Subscription { get; set; }
        public BillingInvoice UpcomingInvoice { get; set; }
        public IEnumerable<BillingCharge> Charges { get; set; }
    }
}
