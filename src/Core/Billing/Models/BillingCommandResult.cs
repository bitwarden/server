using OneOf;

namespace Bit.Core.Billing.Models;

public record BadRequest(string TranslationKey)
{
    public static BadRequest TaxIdNumberInvalid => new(BillingErrorTranslationKeys.TaxIdInvalid);
    public static BadRequest TaxLocationInvalid => new(BillingErrorTranslationKeys.CustomerTaxLocationInvalid);
    public static BadRequest UnknownTaxIdType => new(BillingErrorTranslationKeys.UnknownTaxIdType);
}

public record Unhandled(string TranslationKey = BillingErrorTranslationKeys.UnhandledError);

public class BillingCommandResult<T> : OneOfBase<T, BadRequest, Unhandled>
{
    private BillingCommandResult(OneOf<T, BadRequest, Unhandled> input) : base(input) { }

    public static implicit operator BillingCommandResult<T>(T output) => new(output);
    public static implicit operator BillingCommandResult<T>(BadRequest badRequest) => new(badRequest);
    public static implicit operator BillingCommandResult<T>(Unhandled unhandled) => new(unhandled);
}

public static class BillingErrorTranslationKeys
{
    // "The tax ID number you provided was invalid. Please try again or contact support."
    public const string TaxIdInvalid = "taxIdInvalid";

    // "Your location wasn't recognized. Please ensure your country and postal code are valid and try again."
    public const string CustomerTaxLocationInvalid = "customerTaxLocationInvalid";

    // "Something went wrong with your request. Please contact support."
    public const string UnhandledError = "unhandledBillingError";

    // "We couldn't find a corresponding tax ID type for the tax ID you provided. Please try again or contact support."
    public const string UnknownTaxIdType = "unknownTaxIdType";
}
