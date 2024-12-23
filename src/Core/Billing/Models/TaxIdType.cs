using System.Text.RegularExpressions;

namespace Bit.Core.Billing.Models;

public class TaxIdType
{
    /// <summary>
    /// ISO-3166-2 code for the country.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// The identifier in Stripe for the tax ID type.
    /// </summary>
    public string Code { get; set; }

    public Regex ValidationExpression { get; set; }

    public string Description { get; set; }

    public string Example { get; set; }
}
