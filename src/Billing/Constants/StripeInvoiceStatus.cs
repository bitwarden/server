namespace Bit.Billing.Constants;

public static class StripeInvoiceStatus
{
    /// <summary>
    /// <para>The invoice isn’t ready to use. All invoices start in draft status.</para>
    /// <para>Possible actions:</para>
    /// <list type="bullet">
    /// <item>Edit any part of the invoice</item>
    /// <item>When the invoice is ready to use, finalize it by changing its status to
    /// <see cref="StripeInvoiceStatus.Open">open</see></item>
    /// <item>If the invoice isn’t associated with a subscription,
    /// <a href="https://stripe.com/docs/invoicing/overview?dashboard-or-api=api#deleted">delete it</a></item>
    /// </list>
    /// </summary>
    public const string Draft = "draft";

    /// <summary>
    /// <para>The invoice is finalized and awaiting payment.</para>
    /// <para>Possible actions:</para>
    /// <list type="bullet">
    /// <item>Send the invoice to a customer for payment</item>
    /// <item>
    /// Change <a href="https://stripe.com/docs/invoicing/invoice-edits?testing-method=with-code">only some elements of
    /// the invoice.</a> To make more substantive changes, create a new invoice and void the old one
    /// </item>
    /// <item>Change the invoice’s status to paid, void, or uncollectible.</item>
    /// </list>
    /// </summary>
    public const string Open = "open";

    /// <summary>
    /// <para>The invoice is paid.</para>
    /// <para>Possible actions:</para>
    /// <list type="bullet">
    /// <item>No further actions</item>
    /// </list>
    /// </summary>
    public const string Paid = "paid";

    /// <summary>
    /// <para>The invoice is cancelled.</para>
    /// <para>Possible actions:</para>
    /// <list type="bullet">
    /// <item>No further actions</item>
    /// </list>
    /// </summary>
    public const string Void = "void";

    /// <summary>
    /// <para>
    /// The customer is unlikely to pay the invoice. Normally, you treat it as bad debt in your accounting process.
    /// </para>
    /// <para>Possible actions:</para>
    /// <list type="bullet">
    /// <item>
    /// Change the invoice’s status to <see cref="StripeInvoiceStatus.Void">void</see> or
    /// <see cref="StripeInvoiceStatus.Paid">paid</see>.
    /// </item>
    /// </list>
    /// </summary>
    public const string Uncollectible = "uncollectible";
}
