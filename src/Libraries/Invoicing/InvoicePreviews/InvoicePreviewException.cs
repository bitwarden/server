namespace Bit.Invoicing.InvoicePreviews;

/// <summary>Thrown when a required position (Password Manager seats) cannot be resolved from the source.</summary>
public sealed class InvoicePreviewException(string message) : Exception(message);
