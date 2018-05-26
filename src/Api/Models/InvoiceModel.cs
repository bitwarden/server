using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Table;
using Stripe;

namespace Bit.Api.Models
{
    public class InvoiceModel
    {
        public InvoiceModel(Organization organization, StripeInvoice invoice, ApiSettings apiSettings)
        {
            OurAddress1 = apiSettings.OurAddress1;
            OurAddress2 = apiSettings.OurAddress2;
            OurAddress3 = apiSettings.OurAddress3;

            CustomerName = organization.BusinessName ?? "--";
            CustomerAddress1 = organization.BusinessAddress1;
            CustomerAddress2 = organization.BusinessAddress2;
            CustomerAddress3 = organization.BusinessAddress3;
            CustomerCountry = organization.BusinessCountry;
            CustomerVatNumber = organization.BusinessTaxNumber;

            InvoiceDate = invoice.Date?.ToLongDateString();
            InvoiceDueDate = invoice.DueDate?.ToLongDateString();
            InvoiceNumber = invoice.Number;
            Items = invoice.StripeInvoiceLineItems.Select(i => new Item(i));

            SubtotalAmount = (invoice.Total / 100).ToString("C");
            VatTotalAmount = 0.ToString("C");
            TotalAmount = SubtotalAmount;
            Paid = invoice.Paid;
        }

        public string OurAddress1 { get; set; }
        public string OurAddress2 { get; set; }
        public string OurAddress3 { get; set; }
        public string InvoiceDate { get; set; }
        public string InvoiceDueDate { get; set; }
        public string InvoiceNumber { get; set; }
        public string CustomerName { get; set; }
        public string CustomerVatNumber { get; set; }
        public string CustomerAddress1 { get; set; }
        public string CustomerAddress2 { get; set; }
        public string CustomerAddress3 { get; set; }
        public string CustomerCountry { get; set; }
        public IEnumerable<Item> Items { get; set; }
        public string SubtotalAmount { get; set; }
        public string VatTotalAmount { get; set; }
        public string TotalAmount { get; set; }
        public bool Paid { get; set; }
        public bool UsesVat => !string.IsNullOrWhiteSpace(CustomerVatNumber);

        public class Item
        {
            public Item(StripeInvoiceLineItem item)
            {
                Amount = (item.Amount / 100).ToString("F");
                if(!string.IsNullOrWhiteSpace(item.Description))
                {
                    Description = item.Description;
                }
                else if(!string.IsNullOrWhiteSpace(item.Plan?.Nickname) && item.Quantity.GetValueOrDefault() > 0)
                {
                    Description = $"{item.Quantity} x {item.Plan.Nickname}";
                }
                else
                {
                    Description = "--";
                }
            }

            public string Description { get; set; }
            public string Amount { get; set; }
        }
    }
}
