using System.Collections.Generic;
using System.Linq;
using Bit.Core.Models.Table;
using Stripe;

namespace Bit.Api.Models
{
    public class InvoiceModel
    {
        public InvoiceModel(Organization organization, StripeInvoice invoice)
        {
            // TODO: address
            OurAddress1 = "567 Green St";
            OurAddress2 = "Jacksonville, FL 32256";
            OurAddress3 = "United States";

            CustomerName = organization.BusinessName ?? "--";
            // TODO: address and vat
            CustomerAddress1 = "123 Any St";
            CustomerAddress2 = "New York, NY 10001";
            CustomerAddress3 = "United States";
            CustomerVatNumber = "PT 123456789";

            InvoiceDate = invoice.Date?.ToLongDateString();
            InvoiceDueDate = invoice.DueDate?.ToLongDateString();
            InvoiceNumber = invoice.Id;
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
                Quantity = item.Quantity?.ToString() ?? "-";
                Amount = (item.Amount / 100).ToString("F");
                Description = item.Description ?? "--";
            }

            public string Description { get; set; }
            public string Quantity { get; set; }
            public string Amount { get; set; }
        }
    }
}
