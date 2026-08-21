using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Bit.HttpExtensions.Test;

/// <summary>
/// Every pattern, checked against the message its attribute actually produces.
/// </summary>
/// <remarks>
/// Recognising a message is the whole mechanism, so the wording is the contract. These ask the real attribute to
/// format itself and assert the code that comes back, which turns a framework reword into a failure here on the
/// next SDK bump rather than a silently uncoded error in production.
/// </remarks>
public class ValidationMessageCodesTests
{
    private static ErrorCode Resolve(ValidationAttribute attribute, string name = "Name") =>
        ValidationMessageCodes.Resolve(attribute.FormatErrorMessage(name));

    [Fact]
    public void Required()
    {
        Assert.Equal(ValidationCodes.Required, Resolve(new RequiredAttribute()).Type);
    }

    [Fact]
    public void StringLengthWithOnlyAMaximum_IsTooLongAndCarriesIt()
    {
        var error = Resolve(new StringLengthAttribute(200));

        Assert.Equal(ValidationCodes.TooLong, error.Type);
        Assert.Equal(200, (int)error.Parameters![ValidationParameters.Max]!);
    }

    [Fact]
    public void StringLengthWithBothBounds_IsOneCodeCarryingBoth()
    {
        // The attribute words both directions identically, so there is nothing to tell them apart.
        var error = Resolve(new StringLengthAttribute(200) { MinimumLength = 5 });

        Assert.Equal(ValidationCodes.InvalidLength, error.Type);
        Assert.Equal(5, (int)error.Parameters![ValidationParameters.Min]!);
        Assert.Equal(200, (int)error.Parameters[ValidationParameters.Max]!);
    }

    [Fact]
    public void MaxLength_IsTooLongAndCarriesIt()
    {
        var error = Resolve(new MaxLengthAttribute(100));

        Assert.Equal(ValidationCodes.TooLong, error.Type);
        Assert.Equal(100, (int)error.Parameters![ValidationParameters.Max]!);
    }

    [Fact]
    public void MinLength_IsTooShortAndCarriesIt()
    {
        var error = Resolve(new MinLengthAttribute(1));

        Assert.Equal(ValidationCodes.TooShort, error.Type);
        Assert.Equal(1, (int)error.Parameters![ValidationParameters.Min]!);
    }

    [Fact]
    public void Range_CarriesBothBounds()
    {
        var error = Resolve(new RangeAttribute(1, 100));

        Assert.Equal(ValidationCodes.OutOfRange, error.Type);
        Assert.Equal(1, (int)error.Parameters![ValidationParameters.Min]!);
        Assert.Equal(100, (int)error.Parameters[ValidationParameters.Max]!);
    }

    [Fact]
    public void ARangeStatedInNonNumbers_CarriesTheBoundsAsTheMessageRendered_Them()
    {
        // The limitation of reading bounds out of prose, pinned rather than papered over: what comes back is the
        // framework's rendering of the bound, not the string the attribute was declared with. A client gets
        // something it can display, but not the original literal.
        var error = Resolve(new RangeAttribute(typeof(DateTime), "2020-01-01", "2030-01-01"), "Starts");

        Assert.Equal(ValidationCodes.OutOfRange, error.Type);
        Assert.Equal("2020-01-01 00:00:00", (string)error.Parameters![ValidationParameters.Min]!);
        Assert.Equal("2030-01-01 00:00:00", (string)error.Parameters[ValidationParameters.Max]!);
    }

    [Fact]
    public void EmailAddress()
    {
        Assert.Equal(ValidationCodes.InvalidEmail, Resolve(new EmailAddressAttribute()).Type);
    }

    [Fact]
    public void RegularExpression_CarriesThePattern()
    {
        var error = Resolve(new RegularExpressionAttribute("^a+$"));

        Assert.Equal(ValidationCodes.InvalidFormat, error.Type);
        Assert.Equal("^a+$", (string)error.Parameters![ValidationParameters.Pattern]!);
    }

    [Fact]
    public void Compare_CarriesTheOtherProperty()
    {
        var error = Resolve(new CompareAttribute("Password"), "ConfirmPassword");

        Assert.Equal(ValidationCodes.MustMatch, error.Type);
        Assert.Equal("Password", (string)error.Parameters![ValidationParameters.Other]!);
    }

    [Fact]
    public void Url_IsInvalidFormat() =>
        Assert.Equal(ValidationCodes.InvalidFormat, Resolve(new UrlAttribute()).Type);

    [Fact]
    public void Phone_IsInvalidFormat() =>
        Assert.Equal(ValidationCodes.InvalidFormat, Resolve(new PhoneAttribute()).Type);

    [Fact]
    public void CreditCard_IsInvalidFormat() =>
        Assert.Equal(ValidationCodes.InvalidFormat, Resolve(new CreditCardAttribute()).Type);

    [Fact]
    public void AMessageNoPatternClaims_KeepsItsDetailAndLosesOnlyItsCode()
    {
        // What an explicit ErrorMessage looks like: recognised by nothing, still reported.
        var error = ValidationMessageCodes.Resolve("'key' must be provided");

        Assert.Equal(ValidationCodes.Invalid, error.Type);
        Assert.Equal("'key' must be provided", error.Detail);
        Assert.Null(error.Parameters);
    }

    [Fact]
    public void EveryMessageCarriesItsOriginalDetail()
    {
        var message = new StringLengthAttribute(200).FormatErrorMessage("Name");

        Assert.Equal(message, ValidationMessageCodes.Resolve(message).Detail);
    }

    [Fact]
    public void NullMessage_Throws() =>
        Assert.Throws<ArgumentNullException>(() => ValidationMessageCodes.Resolve(null!));
}
