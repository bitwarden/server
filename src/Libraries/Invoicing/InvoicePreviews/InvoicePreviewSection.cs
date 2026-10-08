namespace Bit.Invoicing.InvoicePreviews;

/// <summary>The <see cref="Models.InvoicePreview"/> section a purchasable reference renders under. Kept separate from Core's ProductType, which also drives trial sign-up.</summary>
internal enum InvoicePreviewSection
{
    PasswordManager,
    SecretsManager,
    PrivilegedControls,
}
