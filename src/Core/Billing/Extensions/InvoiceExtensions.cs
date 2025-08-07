using System.Text.RegularExpressions;
using Stripe;

namespace Bit.Core.Billing.Extensions;

public static class InvoiceExtensions
{
    /// <summary>
    /// Formats invoice line items specifically for provider invoices, standardizing product descriptions
    /// and ensuring consistent tax representation.
    /// </summary>
    /// <param name="invoice">The Stripe invoice containing line items</param>
    /// <param name="subscription">The associated subscription (for future extensibility)</param>
    /// <returns>A list of formatted invoice item descriptions</returns>
    /// <exception cref="ArgumentNullException">Thrown when invoice is null</exception>
    public static List<string> FormatForProvider(this Invoice invoice, Subscription subscription)
    {
        if (invoice == null)
        {
            throw new ArgumentNullException(nameof(invoice));
        }

        var items = new List<string>();

        // Return empty list if no line items
        if (invoice.Lines == null)
        {
            return items;
        }

        foreach (var line in invoice.Lines.Data ?? new List<InvoiceLineItem>())
        {
            // Skip null lines or lines without description
            if (line?.Description == null)
            {
                continue;
            }

            var description = line.Description;

            // Handle Provider Portal service lines
            if (description.Contains("Provider Portal - Teams") || description.Contains("Provider Portal - Enterprise"))
            {
                var priceMatch = Regex.Match(description, @"\(at \$[\d,]+\.?\d* / month\)");
                var priceInfo = priceMatch.Success ? priceMatch.Value : "";

                var standardizedDescription = $"{line.Quantity} × Manage service provider {priceInfo}";
                items.Add(standardizedDescription);
            }
            // Handle tax lines
            else if (description.ToLower().Contains("tax"))
            {
                var priceMatch = Regex.Match(description, @"\(at \$[\d,]+\.?\d* / month\)");
                var priceInfo = priceMatch.Success ? priceMatch.Value : "";

                // If no price info found in description, calculate from amount
                if (string.IsNullOrEmpty(priceInfo) && line.Quantity > 0)
                {
                    var pricePerItem = (line.Amount / 100m) / line.Quantity;
                    priceInfo = $"(at ${pricePerItem:F2} / month)";
                }

                var taxDescription = $"{line.Quantity} × Tax {priceInfo}";
                items.Add(taxDescription);
            }
            // Handle other line items as-is
            else
            {
                items.Add(description);
            }
        }

        // Add fallback tax from invoice-level tax if present and not already included
        if (invoice.Tax.HasValue && invoice.Tax.Value > 0)
        {
            var taxAmount = invoice.Tax.Value / 100m;
            items.Add($"1 × Tax (at ${taxAmount:F2} / month)");
        }

        return items;
    }
}
