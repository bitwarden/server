using System.Globalization;
using Bit.Core.Billing.Entities;
using CsvHelper.Configuration.Attributes;

namespace Bit.Commercial.Core.Billing.Models;

public class ProviderClientInvoiceReportRow
{
    public string Client { get; set; }
    public int Assigned { get; set; }
    public int Used { get; set; }
    public int Remaining { get; set; }
    public string Plan { get; set; }
    [Name("Estimated total")]
    public string Total { get; set; }

    public static ProviderClientInvoiceReportRow From(ProviderInvoiceItem providerInvoiceItem)
        => new()
        {
            Client = providerInvoiceItem.ClientName,
            Assigned = providerInvoiceItem.AssignedSeats,
            Used = providerInvoiceItem.UsedSeats,
            Remaining = providerInvoiceItem.AssignedSeats - providerInvoiceItem.UsedSeats,
            Plan = providerInvoiceItem.PlanName,
            Total = string.Format(new CultureInfo("en-US", false), "{0:C}", providerInvoiceItem.Total)
        };
}
