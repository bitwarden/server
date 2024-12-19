namespace Bit.Core.Billing.Extensions;

public static class CurrencyExtensions
{
    /// <summary>
    /// Converts a currency amount in major units to minor units.
    /// </summary>
    /// <example>123.99 USD returns 12399 in minor units.</example>
    public static long ToMinor(this decimal amount)
    {
        return Convert.ToInt64(amount * 100);
    }

    /// <summary>
    /// Converts a currency amount in minor units to major units.
    /// </summary>
    /// <param name="amount"></param>
    /// <example>12399 in minor units returns 123.99 USD.</example>
    public static decimal? ToMajor(this long? amount)
    {
        return amount?.ToMajor();
    }

    /// <summary>
    /// Converts a currency amount in minor units to major units.
    /// </summary>
    /// <param name="amount"></param>
    /// <example>12399 in minor units returns 123.99 USD.</example>
    public static decimal ToMajor(this long amount)
    {
        return Convert.ToDecimal(amount) / 100;
    }
}
