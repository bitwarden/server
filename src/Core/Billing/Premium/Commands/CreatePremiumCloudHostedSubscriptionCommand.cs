using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Models.Sales;
using Bit.Core.Billing.Payment.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using OneOf.Types;

namespace Bit.Core.Billing.Premium.Commands;

public interface ICreatePremiumCloudHostedSubscriptionCommand
{
    Task<BillingCommandResult<None>> Run(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress,
        short additionalStorageGb);
}

public class CreatePremiumCloudHostedSubscriptionCommand(
    IPremiumUserBillingService premiumUserBillingService,
    IUserService userService,
    IPaymentService paymentService,
    IPushNotificationService pushNotificationService,
    ILogger<CreatePremiumCloudHostedSubscriptionCommand> logger)
    : BaseBillingCommand<CreatePremiumCloudHostedSubscriptionCommand>(logger), ICreatePremiumCloudHostedSubscriptionCommand
{
    public Task<BillingCommandResult<None>> Run(
        User user,
        TokenizedPaymentMethod paymentMethod,
        BillingAddress billingAddress,
        short additionalStorageGb) => HandleAsync<None>(async () =>
    {
        if (user.Premium)
        {
            return new BadRequest("Already a premium user.");
        }

        if (additionalStorageGb < 0)
        {
            return new BadRequest("You can't subtract storage.");
        }

        var paymentMethodType = paymentMethod.Type switch
        {
            TokenizablePaymentMethodType.BankAccount => PaymentMethodType.BankAccount,
            TokenizablePaymentMethodType.Card => PaymentMethodType.Card,
            TokenizablePaymentMethodType.PayPal => PaymentMethodType.PayPal,
            _ => throw new InvalidOperationException($"Unsupported payment method type: {paymentMethod.Type}")
        };

        var taxInfo = new TaxInfo
        {
            BillingAddressCountry = billingAddress.Country,
            BillingAddressPostalCode = billingAddress.PostalCode
        };

        var sale = PremiumUserSale.From(user, paymentMethodType, paymentMethod.Token, taxInfo, additionalStorageGb);
        await premiumUserBillingService.Finalize(sale);

        user.Premium = true;
        user.RevisionDate = DateTime.UtcNow;
        user.MaxStorageGb = (short)(1 + additionalStorageGb);
        user.LicenseKey = CoreHelpers.SecureRandomString(20);

        try
        {
            await userService.SaveUserAsync(user);
            await pushNotificationService.PushSyncVaultAsync(user.Id);
        }
        catch
        {
            await paymentService.CancelAndRecoverChargesAsync(user);
            throw;
        }

        return new None();
    });
}
