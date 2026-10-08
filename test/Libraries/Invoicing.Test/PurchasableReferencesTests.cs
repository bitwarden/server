using System.Reflection;
using Bit.Core.Billing.Constants;
using Bit.Invoicing.InvoicePreviews;
using Xunit;

namespace Bit.Invoicing.Test;

public class PurchasableReferencesTests
{
    [Theory]
    [InlineData(StripeConstants.PurchasableReferences.PasswordManagerSeat)]
    [InlineData(StripeConstants.PurchasableReferences.PasswordManagerStorage)]
    [InlineData(StripeConstants.PurchasableReferences.SecretsManagerSeat)]
    [InlineData(StripeConstants.PurchasableReferences.SecretsManagerServiceAccount)]
    [InlineData(StripeConstants.PurchasableReferences.PrivilegedControlsSeat)]
    public void IsKnown_TrueForKnownReference(string reference)
        => Assert.True(PurchasableReferences.IsKnown(reference));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("provider-seat")]
    [InlineData("PM-SEAT")]
    [InlineData("pc-seat")]
    [InlineData("PAM-SEAT")]
    public void IsKnown_FalseForUnknownReference(string? reference)
        => Assert.False(PurchasableReferences.IsKnown(reference));

    // InvoicePreviewSection is internal, so a public theory can't take it as a parameter; the expected value is passed by name.
    [Theory]
    [InlineData(StripeConstants.PurchasableReferences.PasswordManagerSeat, nameof(InvoicePreviewSection.PasswordManager))]
    [InlineData(StripeConstants.PurchasableReferences.PasswordManagerStorage, nameof(InvoicePreviewSection.PasswordManager))]
    [InlineData(StripeConstants.PurchasableReferences.SecretsManagerSeat, nameof(InvoicePreviewSection.SecretsManager))]
    [InlineData(StripeConstants.PurchasableReferences.SecretsManagerServiceAccount, nameof(InvoicePreviewSection.SecretsManager))]
    [InlineData(StripeConstants.PurchasableReferences.PrivilegedControlsSeat, nameof(InvoicePreviewSection.PrivilegedControls))]
    public void SectionOf_MapsReferenceToSection(string reference, string expected)
        => Assert.Equal(Enum.Parse<InvoicePreviewSection>(expected), PurchasableReferences.SectionOf(reference));

    [Fact]
    public void SectionOf_UnknownReference_ReturnsNull()
        => Assert.Null(PurchasableReferences.SectionOf("fake-reference"));

    // Guards the three-place edit (enum, map, builder): a section with no reference would route nothing.
    [Fact]
    public void SectionOf_EverySectionIsReachableFromAConstant()
    {
        var reachable = typeof(StripeConstants.PurchasableReferences)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => PurchasableReferences.SectionOf((string)field.GetRawConstantValue()!))
            .ToHashSet();

        Assert.All(Enum.GetValues<InvoicePreviewSection>(), section => Assert.Contains(section, reachable));
    }
}
