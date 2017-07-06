using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using Stripe;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Bit.Core.Utilities
{
    public static class BillingHelpers
    {
        internal static async Task CancelAndRecoverChargesAsync(string subscriptionId, string customerId)
        {
            if(!string.IsNullOrWhiteSpace(subscriptionId))
            {
                var subscriptionService = new StripeSubscriptionService();
                await subscriptionService.CancelAsync(subscriptionId, false);
            }

            if(string.IsNullOrWhiteSpace(customerId))
            {
                return;
            }

            var chargeService = new StripeChargeService();
            var charges = await chargeService.ListAsync(new StripeChargeListOptions { CustomerId = customerId });
            if(charges?.Data != null)
            {
                var refundService = new StripeRefundService();
                foreach(var charge in charges.Data.Where(c => !c.Refunded))
                {
                    await refundService.CreateAsync(charge.Id);
                }
            }

            var customerService = new StripeCustomerService();
            await customerService.DeleteAsync(customerId);
        }

        public static async Task<BillingInfo> GetBillingAsync(ISubscriber subscriber)
        {
            var orgBilling = new BillingInfo();
            var customerService = new StripeCustomerService();
            var subscriptionService = new StripeSubscriptionService();
            var chargeService = new StripeChargeService();
            var invoiceService = new StripeInvoiceService();

            if(!string.IsNullOrWhiteSpace(subscriber.StripeSubscriptionId))
            {
                var customer = await customerService.GetAsync(subscriber.StripeCustomerId);
                if(customer != null)
                {
                    if(!string.IsNullOrWhiteSpace(customer.DefaultSourceId) && customer.Sources?.Data != null)
                    {
                        if(customer.DefaultSourceId.StartsWith("card_"))
                        {
                            orgBilling.PaymentSource =
                                customer.Sources.Data.FirstOrDefault(s => s.Card?.Id == customer.DefaultSourceId);
                        }
                        else if(customer.DefaultSourceId.StartsWith("ba_"))
                        {
                            orgBilling.PaymentSource =
                                customer.Sources.Data.FirstOrDefault(s => s.BankAccount?.Id == customer.DefaultSourceId);
                        }
                    }

                    var charges = await chargeService.ListAsync(new StripeChargeListOptions
                    {
                        CustomerId = customer.Id,
                        Limit = 20
                    });
                    orgBilling.Charges = charges?.Data?.OrderByDescending(c => c.Created);
                }
            }

            if(!string.IsNullOrWhiteSpace(subscriber.StripeSubscriptionId))
            {
                var sub = await subscriptionService.GetAsync(subscriber.StripeSubscriptionId);
                if(sub != null)
                {
                    orgBilling.Subscription = sub;
                }

                if(!sub.CanceledAt.HasValue && !string.IsNullOrWhiteSpace(subscriber.StripeCustomerId))
                {
                    try
                    {
                        var upcomingInvoice = await invoiceService.UpcomingAsync(subscriber.StripeCustomerId);
                        if(upcomingInvoice != null)
                        {
                            orgBilling.UpcomingInvoice = upcomingInvoice;
                        }
                    }
                    catch(StripeException) { }
                }
            }

            return orgBilling;
        }

        internal static async Task PreviewUpcomingInvoiceAndPayAsync(ISubscriber subscriber, string planId,
            int prorateThreshold = 500)
        {
            var invoiceService = new StripeInvoiceService();
            var upcomingPreview = await invoiceService.UpcomingAsync(subscriber.StripeCustomerId,
                new StripeUpcomingInvoiceOptions
                {
                    SubscriptionId = subscriber.StripeSubscriptionId
                });

            var prorationAmount = upcomingPreview.StripeInvoiceLineItems?.Data?
                .TakeWhile(i => i.Plan.Id == planId && i.Proration).Sum(i => i.Amount);
            if(prorationAmount.GetValueOrDefault() >= prorateThreshold)
            {
                try
                {
                    // Owes more than prorateThreshold on next invoice.
                    // Invoice them and pay now instead of waiting until next month.
                    var invoice = await invoiceService.CreateAsync(subscriber.StripeCustomerId,
                        new StripeInvoiceCreateOptions
                        {
                            SubscriptionId = subscriber.StripeSubscriptionId
                        });

                    if(invoice.AmountDue > 0)
                    {
                        await invoiceService.PayAsync(invoice.Id);
                    }
                }
                catch(StripeException) { }
            }
        }

        internal static async Task AdjustStorageAsync(IStorableSubscriber storableSubscriber, short storageAdjustmentGb,
            string storagePlanId)
        {
            if(storableSubscriber == null)
            {
                throw new ArgumentNullException(nameof(storableSubscriber));
            }

            if(string.IsNullOrWhiteSpace(storableSubscriber.StripeCustomerId))
            {
                throw new BadRequestException("No payment method found.");
            }

            if(string.IsNullOrWhiteSpace(storableSubscriber.StripeSubscriptionId))
            {
                throw new BadRequestException("No subscription found.");
            }

            if(!storableSubscriber.MaxStorageGb.HasValue)
            {
                throw new BadRequestException("No access to storage.");
            }

            var newStorageGb = (short)(storableSubscriber.MaxStorageGb.Value + storageAdjustmentGb);
            if(newStorageGb < 1)
            {
                newStorageGb = 1;
            }

            if(newStorageGb > 100)
            {
                throw new BadRequestException("Maximum storage is 100 GB.");
            }

            var remainingStorage = storableSubscriber.StorageBytesRemaining(newStorageGb);
            if(remainingStorage < 0)
            {
                throw new BadRequestException("You are currently using " +
                    $"{CoreHelpers.ReadableBytesSize(storableSubscriber.Storage.GetValueOrDefault(0))} of storage. " +
                    "Delete some stored data first.");
            }

            var additionalStorage = newStorageGb - 1;
            var subscriptionItemService = new StripeSubscriptionItemService();
            var subscriptionService = new StripeSubscriptionService();
            var sub = await subscriptionService.GetAsync(storableSubscriber.StripeSubscriptionId);
            if(sub == null)
            {
                throw new BadRequestException("Subscription not found.");
            }

            var seatItem = sub.Items?.Data?.FirstOrDefault(i => i.Plan.Id == storagePlanId);
            if(seatItem == null)
            {
                await subscriptionItemService.CreateAsync(new StripeSubscriptionItemCreateOptions
                {
                    PlanId = storagePlanId,
                    Quantity = additionalStorage,
                    Prorate = true,
                    SubscriptionId = sub.Id
                });
            }
            else if(additionalStorage > 0)
            {
                await subscriptionItemService.UpdateAsync(seatItem.Id, new StripeSubscriptionItemUpdateOptions
                {
                    PlanId = storagePlanId,
                    Quantity = additionalStorage,
                    Prorate = true
                });
            }
            else if(additionalStorage == 0)
            {
                await subscriptionItemService.DeleteAsync(storagePlanId);
            }

            if(additionalStorage > 0)
            {
                await PreviewUpcomingInvoiceAndPayAsync(storableSubscriber, storagePlanId, 300);
            }

            storableSubscriber.MaxStorageGb = newStorageGb;
        }

        public static async Task<bool> UpdatePaymentMethodAsync(ISubscriber subscriber, string paymentToken)
        {
            if(subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            var updatedSubscriber = false;

            var cardService = new StripeCardService();
            var customerService = new StripeCustomerService();
            StripeCustomer customer = null;

            if(!string.IsNullOrWhiteSpace(subscriber.StripeCustomerId))
            {
                customer = await customerService.GetAsync(subscriber.StripeCustomerId);
            }

            if(customer == null)
            {
                customer = await customerService.CreateAsync(new StripeCustomerCreateOptions
                {
                    Description = subscriber.BillingName(),
                    Email = subscriber.BillingEmailAddress(),
                    SourceToken = paymentToken
                });

                subscriber.StripeCustomerId = customer.Id;
                updatedSubscriber = true;
            }

            await cardService.CreateAsync(customer.Id, new StripeCardCreateOptions
            {
                SourceToken = paymentToken
            });

            if(!string.IsNullOrWhiteSpace(customer.DefaultSourceId))
            {
                await cardService.DeleteAsync(customer.Id, customer.DefaultSourceId);
            }

            return updatedSubscriber;
        }

        public static async Task CancelSubscriptionAsync(ISubscriber subscriber, bool endOfPeriod = false)
        {
            if(subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            if(string.IsNullOrWhiteSpace(subscriber.StripeSubscriptionId))
            {
                throw new BadRequestException("No subscription.");
            }

            var subscriptionService = new StripeSubscriptionService();
            var sub = await subscriptionService.GetAsync(subscriber.StripeSubscriptionId);
            if(sub == null)
            {
                throw new BadRequestException("Subscription was not found.");
            }

            if(sub.CanceledAt.HasValue)
            {
                throw new BadRequestException("Subscription is already canceled.");
            }

            var canceledSub = await subscriptionService.CancelAsync(sub.Id, endOfPeriod);
            if(!canceledSub.CanceledAt.HasValue)
            {
                throw new BadRequestException("Unable to cancel subscription.");
            }
        }

        public static async Task ReinstateSubscriptionAsync(ISubscriber subscriber)
        {
            if(subscriber == null)
            {
                throw new ArgumentNullException(nameof(subscriber));
            }

            if(string.IsNullOrWhiteSpace(subscriber.StripeSubscriptionId))
            {
                throw new BadRequestException("No subscription.");
            }

            var subscriptionService = new StripeSubscriptionService();
            var sub = await subscriptionService.GetAsync(subscriber.StripeSubscriptionId);
            if(sub == null)
            {
                throw new BadRequestException("Subscription was not found.");
            }

            if(sub.Status != "active" || !sub.CanceledAt.HasValue)
            {
                throw new BadRequestException("Subscription is not marked for cancellation.");
            }

            // Just touch the subscription.
            var updatedSub = await subscriptionService.UpdateAsync(sub.Id, new StripeSubscriptionUpdateOptions { });
            if(updatedSub.CanceledAt.HasValue)
            {
                throw new BadRequestException("Unable to reinstate subscription.");
            }
        }
    }
}
