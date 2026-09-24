using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Test.Billing.Mocks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Commands;

using static StripeConstants;

public class UpdateOrganizationSubscriptionCommandTests
{
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly IPricingClient _pricingClient = Substitute.For<IPricingClient>();
    private readonly IOrganizationPlanMigrationCohortAssignmentRepository _assignmentRepository =
        Substitute.For<IOrganizationPlanMigrationCohortAssignmentRepository>();
    private readonly IOrganizationPlanMigrationCohortRepository _cohortRepository =
        Substitute.For<IOrganizationPlanMigrationCohortRepository>();
    private readonly UpdateOrganizationSubscriptionCommand _command;

    public UpdateOrganizationSubscriptionCommandTests()
    {
        // Default: no cohort assignment, so tests take the non-migration path unless SetupMigration is called.
        _assignmentRepository.GetByOrganizationIdAsync(Arg.Any<Guid>())
            .Returns((OrganizationPlanMigrationCohortAssignment?)null);

        _command = new UpdateOrganizationSubscriptionCommand(
            Substitute.For<ILogger<UpdateOrganizationSubscriptionCommand>>(),
            _assignmentRepository,
            _cohortRepository,
            _pricingClient,
            _stripeAdapter);
    }

    [Fact]
    public async Task Run_SubscriptionNotFound_ReturnsBadRequest()
    {
        var organization = CreateOrganization();

        _stripeAdapter
            .GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns<Subscription>(_ => throw new StripeException { StripeError = new StripeError { Code = ErrorCodes.ResourceMissing } });

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.IsT1);
        Assert.Equal("We couldn't find your subscription.", result.AsT1.Response);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Canceled)]
    [InlineData(SubscriptionStatus.Incomplete)]
    [InlineData(SubscriptionStatus.IncompleteExpired)]
    [InlineData(SubscriptionStatus.Unpaid)]
    [InlineData(SubscriptionStatus.Paused)]
    public async Task Run_InvalidSubscriptionStatus_ReturnsBadRequest(string status)
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(status: status, items: [("price_seats", "si_1", 5)]);

        _stripeAdapter
            .GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.IsT1);
        Assert.Equal("Your subscription cannot be updated in its current status.", result.AsT1.Response);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Active)]
    [InlineData(SubscriptionStatus.Trialing)]
    [InlineData(SubscriptionStatus.PastDue)]
    public async Task Run_ValidSubscriptionStatus_DoesNotReturnStatusError(string status)
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(status: status, items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Run_FetchesSubscriptionWithCustomerDiscountSourceCouponExpanded()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        await _command.Run(organization, changeSet);

        await _stripeAdapter.Received().GetSubscriptionAsync(
            organization.GatewaySubscriptionId,
            Arg.Is<SubscriptionGetOptions>(o => o.Expand.Contains("customer.discount.source.coupon")));
    }

    [Fact]
    public async Task Run_EmptyChangeSet_ReturnsConflict()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);

        var changeSet = new OrganizationSubscriptionChangeSet { Changes = [] };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.IsT2);
        Assert.Equal("No changes were provided for the organization subscription update", result.AsT2.Response);
    }

    [Fact]
    public async Task Run_AddItem_DuplicatePrice_ReturnsBadRequest()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.IsT1);
        Assert.Contains("price_seats", result.AsT1.Response);
    }

    [Fact]
    public async Task Run_AddItem_Valid_CreatesCorrectOptions()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem("price_storage", 3)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Items.Count == 1 &&
                options.Items[0].Price == "price_storage" &&
                options.Items[0].Quantity == 3));
    }

    [Fact]
    public async Task Run_ChangeItemPrice_MissingCurrentPrice_ReturnsBadRequest()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new ChangeItemPrice("price_nonexistent", "price_new", null)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.IsT1);
        Assert.Contains("price_nonexistent", result.AsT1.Response);
    }

    [Fact]
    public async Task Run_ChangeItemPrice_Valid_PreservesExistingQuantity()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_monthly", "si_1", 10)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new ChangeItemPrice("price_monthly", "price_annual", null)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Items.Count == 1 &&
                options.Items[0].Id == "si_1" &&
                options.Items[0].Price == "price_annual" &&
                options.Items[0].Quantity == 10));
    }

    [Fact]
    public async Task Run_ChangeItemPrice_WithExplicitQuantity_UsesProvidedQuantity()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_monthly", "si_1", 10)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new ChangeItemPrice("price_monthly", "price_annual", 20)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Items[0].Quantity == 20));
    }

    [Fact]
    public async Task Run_RemoveItem_MissingPrice_ReturnsBadRequest()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new RemoveItem("price_nonexistent")]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.IsT1);
        Assert.Contains("price_nonexistent", result.AsT1.Response);
    }

    [Fact]
    public async Task Run_RemoveItem_Valid_SetsDeletedTrue()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5), ("price_storage", "si_2", 1)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new RemoveItem("price_storage")]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Items.Count == 1 &&
                options.Items[0].Id == "si_2" &&
                options.Items[0].Deleted == true));
    }

    [Fact]
    public async Task Run_StripeExceptionDuringUpdate_ReturnsUnhandled()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);

        _stripeAdapter
            .UpdateSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionUpdateOptions>())
            .Returns<Subscription>(_ => throw new StripeException { StripeError = new StripeError { Code = "api_error" } });

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.IsT3);
    }

    [Fact]
    public async Task Run_UpdateItemQuantity_MissingPrice_ReturnsBadRequest()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_nonexistent", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.IsT1);
        Assert.Contains("price_nonexistent", result.AsT1.Response);
    }

    [Fact]
    public async Task Run_UpdateItemQuantity_Valid_CreatesCorrectOptions()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 15)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Items.Count == 1 &&
                options.Items[0].Id == "si_1" &&
                options.Items[0].Price == "price_seats" &&
                options.Items[0].Quantity == 15));
    }

    [Fact]
    public async Task Run_UpdateItemQuantity_ZeroQuantity_SetsDeletedTrue()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 0)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.Items[0].Id == "si_1" &&
                options.Items[0].Deleted == true));
    }

    [Fact]
    public async Task Run_ChargeImmediately_SetsAlwaysInvoiceProration()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem("price_storage", 1)],
            ChargeImmediately = true
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.ProrationBehavior == ProrationBehavior.AlwaysInvoice));
    }

    [Fact]
    public async Task Run_NotChargeImmediately_SetsCreateProrationsProration()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.ProrationBehavior == ProrationBehavior.CreateProrations));
    }

    [Fact]
    public async Task Run_ChargeImmediately_ChargeAutomatically_SetsPendingIfIncomplete()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(
            collectionMethod: CollectionMethod.ChargeAutomatically,
            items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem("price_storage", 1)],
            ChargeImmediately = true
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.PaymentBehavior == PaymentBehavior.PendingIfIncomplete));
    }

    [Fact]
    public async Task Run_ChargeImmediately_SendInvoice_NoPaymentBehavior()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(
            collectionMethod: CollectionMethod.SendInvoice,
            items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem("price_storage", 1)],
            ChargeImmediately = true
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.PaymentBehavior == null));
    }

    [Fact]
    public async Task Run_NotChargeImmediately_ChargeAutomatically_NoPaymentBehavior()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(
            collectionMethod: CollectionMethod.ChargeAutomatically,
            items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.PaymentBehavior == null));
    }

    [Fact]
    public async Task Run_AnnualBilling_NonStructural_Active_SetsPendingInvoiceItemInterval()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(
            status: SubscriptionStatus.Active,
            billingInterval: Intervals.Year,
            items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.PendingInvoiceItemInterval != null &&
                options.PendingInvoiceItemInterval.Interval == Intervals.Month));
    }

    [Fact]
    public async Task Run_AnnualBilling_NonStructural_Trialing_NoPendingInvoiceItemInterval()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(
            status: SubscriptionStatus.Trialing,
            billingInterval: Intervals.Year,
            items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.PendingInvoiceItemInterval == null));
    }

    [Fact]
    public async Task Run_AnnualBilling_ChargeImmediately_NoPendingInvoiceItemInterval()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(
            status: SubscriptionStatus.Active,
            billingInterval: Intervals.Year,
            items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem("price_storage", 1)],
            ChargeImmediately = true
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.PendingInvoiceItemInterval == null));
    }

    [Fact]
    public async Task Run_MonthlyBilling_NonStructural_NoPendingInvoiceItemInterval()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(
            billingInterval: Intervals.Month,
            items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.PendingInvoiceItemInterval == null));
    }

    [Fact]
    public async Task Run_SendInvoice_Structural_DraftInvoice_FinalizesAndSends()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(
            collectionMethod: CollectionMethod.SendInvoice,
            items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);

        var updatedSubscription = CreateSubscription(
            collectionMethod: CollectionMethod.SendInvoice,
            items: [("price_seats", "si_1", 5), ("price_storage", "si_2", 1)]);
        updatedSubscription.LatestInvoiceId = "inv_123";

        _stripeAdapter
            .UpdateSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionUpdateOptions>())
            .Returns(updatedSubscription);

        var draftInvoice = new Invoice { Id = "inv_123", Status = InvoiceStatus.Draft };
        _stripeAdapter.GetInvoiceAsync("inv_123", Arg.Any<InvoiceGetOptions>()).Returns(draftInvoice);

        var finalizedInvoice = new Invoice { Id = "inv_123", Status = InvoiceStatus.Open };
        _stripeAdapter
            .FinalizeInvoiceAsync("inv_123", Arg.Is<InvoiceFinalizeOptions>(o => o.AutoAdvance == false))
            .Returns(finalizedInvoice);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem("price_storage", 1)],
            ChargeImmediately = true
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).GetInvoiceAsync("inv_123", Arg.Any<InvoiceGetOptions>());
        await _stripeAdapter.Received(1).FinalizeInvoiceAsync("inv_123", Arg.Any<InvoiceFinalizeOptions>());
        await _stripeAdapter.Received(1).SendInvoiceAsync("inv_123");
    }

    [Fact]
    public async Task Run_SendInvoice_ChargeImmediately_NonDraftInvoice_DoesNotFinalizeOrSend()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(
            collectionMethod: CollectionMethod.SendInvoice,
            items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);

        var updatedSubscription = CreateSubscription(
            collectionMethod: CollectionMethod.SendInvoice,
            items: [("price_seats", "si_1", 5), ("price_storage", "si_2", 1)]);
        updatedSubscription.LatestInvoiceId = "inv_123";

        _stripeAdapter
            .UpdateSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionUpdateOptions>())
            .Returns(updatedSubscription);

        var openInvoice = new Invoice { Id = "inv_123", Status = InvoiceStatus.Open };
        _stripeAdapter.GetInvoiceAsync("inv_123", Arg.Any<InvoiceGetOptions>()).Returns(openInvoice);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem("price_storage", 1)],
            ChargeImmediately = true
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).GetInvoiceAsync("inv_123", Arg.Any<InvoiceGetOptions>());
        await _stripeAdapter.DidNotReceive().FinalizeInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceFinalizeOptions>());
        await _stripeAdapter.DidNotReceive().SendInvoiceAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Run_ChargeAutomatically_ChargeImmediately_DoesNotProcessInvoice()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(
            collectionMethod: CollectionMethod.ChargeAutomatically,
            items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);

        var updatedSubscription = CreateSubscription(
            collectionMethod: CollectionMethod.ChargeAutomatically,
            items: [("price_seats", "si_1", 5), ("price_storage", "si_2", 1)]);
        updatedSubscription.LatestInvoiceId = "inv_123";

        _stripeAdapter
            .UpdateSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionUpdateOptions>())
            .Returns(updatedSubscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem("price_storage", 1)],
            ChargeImmediately = true
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.DidNotReceive().GetInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceGetOptions>());
        await _stripeAdapter.DidNotReceive().FinalizeInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceFinalizeOptions>());
        await _stripeAdapter.DidNotReceive().SendInvoiceAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Run_SendInvoice_NotChargeImmediately_DoesNotProcessInvoice()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(
            collectionMethod: CollectionMethod.SendInvoice,
            items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.DidNotReceive().GetInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceGetOptions>());
    }

    [Fact]
    public async Task Run_MismatchedTaxExempt_DoesNotReconcile()
    {
        var customer = new Customer
        {
            Id = "cus_123",
            Address = new Address { Country = "DE" },
            TaxExempt = TaxExempt.None
        };

        var organization = CreateOrganization();
        var subscription = CreateSubscription(customer: customer, items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        await _command.Run(organization, new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        });

        await _stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            customer.Id, Arg.Is<CustomerUpdateOptions>(o => o.TaxExempt != null));
    }

    [Fact]
    public async Task Run_MultipleChanges_AllValid_CreatesAllItems()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items:
        [
            ("price_seats", "si_1", 5),
            ("price_monthly", "si_2", 5)
        ]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes =
            [
                new UpdateItemQuantity("price_seats", 10),
                new ChangeItemPrice("price_monthly", "price_annual", null),
                new AddItem("price_storage", 1)
            ]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options => options.Items.Count == 3));
    }

    [Fact]
    public async Task Run_MultipleChanges_SecondInvalid_ReturnsBadRequest()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes =
            [
                new UpdateItemQuantity("price_seats", 10),
                new RemoveItem("price_nonexistent")
            ]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.IsT1);

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task Run_BusinessMigration_CustomerDiscount_OmittedFromActivePhase_CarriedOntoFuture()
    {
        // Discount stacking is migration-only, so this is set up as a migration org.
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var sourceSeat = source.PasswordManager.StripeSeatPlanId;
        var targetSeat = target.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(
            customer: new Customer
            {
                Id = "cus_123",
                Address = new Address { Country = "US" },
                TaxExempt = TaxExempt.None,
                Discount = new Discount { Source = new DiscountSource { Coupon = new Coupon { Id = "retention" } } }
            },
            items: [(sourceSeat, "si_1", 5)]);

        // A live subscription discount on the active phase; carried forward by discount id.
        subscription.Discounts =
            [new Discount { Id = "di_live", Source = new DiscountSource { Coupon = new Coupon { Id = "live-coupon" } } }];

        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(subscription.Id, [(sourceSeat, 5)], [(targetSeat, 5)],
            phaseMetadata: new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort-1" });
        schedule.Phases[1].Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "migration-coupon" }];
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem(source.PasswordManager.StripeStoragePlanId, 3)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                // The active phase carries only live subscription discounts by id; the customer coupon is
                // omitted so it isn't stacked onto the current period. The future phase re-lists the customer
                // coupon (or it would drop off) plus the preserved migration coupon.
                opts.Phases[0].Discounts != null &&
                opts.Phases[0].Discounts.Count == 1 &&
                opts.Phases[0].Discounts[0].Discount == "di_live" &&
                opts.Phases[0].Discounts.All(d => d.Coupon != "retention") &&
                opts.Phases[1].Discounts.Any(d => d.Coupon == "retention") &&
                opts.Phases[1].Discounts.Any(d => d.Coupon == "migration-coupon")));
    }

    [Fact]
    public async Task Run_BusinessMigration_SkipsInvoiceProcessing()
    {
        // The migration rewrite returns early, before the send-invoice finalization path.
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var subscription = CreateSubscription(
            collectionMethod: CollectionMethod.SendInvoice,
            items: [(source.PasswordManager.StripeSeatPlanId, "si_1", 5)]);

        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id,
            [(source.PasswordManager.StripeSeatPlanId, 5)],
            [(target.PasswordManager.StripeSeatPlanId, 5)],
            phaseMetadata: new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort-1" });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem(source.PasswordManager.StripeStoragePlanId, 1)],
            ChargeImmediately = true
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
        await _stripeAdapter.DidNotReceive().GetInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceGetOptions>());
        await _stripeAdapter.DidNotReceive().FinalizeInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceFinalizeOptions>());
        await _stripeAdapter.DidNotReceive().SendInvoiceAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Run_BusinessMigration_AddItem_PhaseSpecificTranslation()
    {
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var subscription = CreateSubscription(items: [(source.PasswordManager.StripeSeatPlanId, "si_1", 10)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id,
            [(source.PasswordManager.StripeSeatPlanId, 10)],
            [(target.PasswordManager.StripeSeatPlanId, 10)],
            phaseMetadata: new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort-1" });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem(source.SecretsManager.StripeServiceAccountPlanId, 5)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases.Count == 2 &&
                opts.Phases[0].Items.Any(i =>
                    i.Price == source.SecretsManager.StripeServiceAccountPlanId && i.Quantity == 5) &&
                opts.Phases[1].Items.Any(i =>
                    i.Price == target.SecretsManager.StripeServiceAccountPlanId && i.Quantity == 5)));
    }

    [Fact]
    public async Task Run_BusinessMigration_ChangeItemPrice_QuantityOnly_TranslatesBothIds()
    {
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var sourceSeat = source.PasswordManager.StripeSeatPlanId;
        var targetSeat = target.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(sourceSeat, "si_1", 10)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id,
            [(sourceSeat, 10)],
            [(targetSeat, 10)],
            phaseMetadata: new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort-1" });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new ChangeItemPrice(sourceSeat, sourceSeat, 20)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Items.Any(i => i.Price == sourceSeat && i.Quantity == 20) &&
                opts.Phases[1].Items.Any(i => i.Price == targetSeat && i.Quantity == 20)));
    }

    [Fact]
    public async Task Run_BusinessMigration_RemoveItem_TranslatesOnPhase2()
    {
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var sourceSeat = source.PasswordManager.StripeSeatPlanId;
        var targetSeat = target.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(sourceSeat, "si_1", 10)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id,
            [(sourceSeat, 10)],
            [(targetSeat, 10)],
            phaseMetadata: new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort-1" });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new RemoveItem(sourceSeat)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Items.All(i => i.Price != sourceSeat) &&
                opts.Phases[1].Items.All(i => i.Price != targetSeat)));
    }

    [Fact]
    public async Task Run_BusinessMigration_UpdateItemQuantity_Zero_TranslatesRemovalOnPhase2()
    {
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var sourceSa = source.SecretsManager.StripeServiceAccountPlanId;
        var targetSa = target.SecretsManager.StripeServiceAccountPlanId;

        var subscription = CreateSubscription(items: [(sourceSa, "si_1", 5)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id,
            [(sourceSa, 5)],
            [(targetSa, 5)],
            phaseMetadata: new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort-1" });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(sourceSa, 0)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Items.All(i => i.Price != sourceSa) &&
                opts.Phases[1].Items.All(i => i.Price != targetSa)));
    }

    [Fact]
    public async Task Run_BusinessMigration_MultipleChanges_TranslatesAcrossSequence()
    {
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var sourceSeat = source.PasswordManager.StripeSeatPlanId;
        var targetSeat = target.PasswordManager.StripeSeatPlanId;
        var sourceSa = source.SecretsManager.StripeServiceAccountPlanId;
        var targetSa = target.SecretsManager.StripeServiceAccountPlanId;
        var sourceStorage = source.PasswordManager.StripeStoragePlanId;
        var targetStorage = target.PasswordManager.StripeStoragePlanId;

        var subscription = CreateSubscription(items:
        [
            (sourceSeat, "si_1", 5),
            (sourceSa, "si_2", 3)
        ]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id,
            [(sourceSeat, 5), (sourceSa, 3)],
            [(targetSeat, 5), (targetSa, 3)],
            phaseMetadata: new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort-1" });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes =
            [
                new UpdateItemQuantity(sourceSeat, 10),
                new RemoveItem(sourceSa),
                new AddItem(sourceStorage, 1)
            ]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Items.Any(i => i.Price == sourceSeat && i.Quantity == 10) &&
                opts.Phases[0].Items.All(i => i.Price != sourceSa) &&
                opts.Phases[0].Items.Any(i => i.Price == sourceStorage && i.Quantity == 1) &&
                opts.Phases[1].Items.Any(i => i.Price == targetSeat && i.Quantity == 10) &&
                opts.Phases[1].Items.All(i => i.Price != targetSa) &&
                opts.Phases[1].Items.Any(i => i.Price == targetStorage && i.Quantity == 1)));
    }

    [Fact]
    public async Task Run_BusinessMigration_SinglePhaseSourcePriced_PreservesPricingAndDiscount()
    {
        // Legacy scenario: a single source-priced phase remains (e.g. cancellation flow left the
        // schedule unreleased). count == 1 alone would mis-classify this as post-migration and
        // wrongly translate prices + clear the migration coupon. The IsPostMigrationPhase check
        // requires items to actually use target-plan price IDs, so this stays source-priced.
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var sourceSeat = source.PasswordManager.StripeSeatPlanId;
        var subscription = CreateSubscription(items: [(sourceSeat, "si_1", 10)]);
        // This lone phase is the active phase, so its coupon is a live subscription discount (di_ id),
        // not a phase-scoped one.
        subscription.Discounts =
        [
            new Discount { Id = "di_migration", Source = new DiscountSource { Coupon = new Coupon { Id = "migration-coupon" } } }
        ];
        SetupGetSubscription(organization, subscription);

        var now = DateTime.UtcNow;
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = subscription.Id,
            Status = SubscriptionScheduleStatus.Active,
            EndBehavior = SubscriptionScheduleEndBehavior.Release,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = now.AddDays(-30),
                    EndDate = now.AddMinutes(-5),
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_anchor", Quantity = 1 }],
                    // Expired, so excluded from the update; carries the marker for ownership classification.
                    Metadata = new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort-1" },
                    ProrationBehavior = ProrationBehavior.None
                },
                new SubscriptionSchedulePhase
                {
                    StartDate = now.AddMinutes(-5),
                    EndDate = now.AddDays(7),
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = sourceSeat, Quantity = 10 }],
                    ProrationBehavior = ProrationBehavior.None
                }
            ]
        };
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(sourceSeat, 20)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases.Count == 1 &&
                opts.Phases[0].Items.Any(i => i.Price == sourceSeat && i.Quantity == 20) &&
                opts.Phases[0].Discounts != null &&
                opts.Phases[0].Discounts.Any(d => d.Discount == "di_migration")));
    }

    [Fact]
    public async Task Run_BusinessMigration_SinglePhasePostMigration_NoEmptyDiscountArray()
    {
        // A single remaining phase already priced on the target plan (post-migration) with no
        // customer or subscription discounts to carry must leave Discounts null, not an empty
        // array -- an empty array deletes whatever discount Stripe is currently applying.
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var targetSeat = target.PasswordManager.StripeSeatPlanId;
        var subscription = CreateSubscription(items: [(targetSeat, "si_1", 10)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id,
            [(targetSeat, 10)],
            phaseMetadata: new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort-1" });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(targetSeat, 20)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases.Count == 1 &&
                opts.Phases[0].Discounts == null));
    }

    [Fact]
    public async Task Run_BusinessMigration_ItemLevelCoupon_SurvivesPhaseItemRebuild()
    {
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var sourceSeat = source.PasswordManager.StripeSeatPlanId;
        var targetSeat = target.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(sourceSeat, "si_1", 10)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id,
            [(sourceSeat, 10)],
            [(targetSeat, 10)],
            phaseMetadata: new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort-1" });
        schedule.Phases[0].Items[0].Discounts = [new SubscriptionSchedulePhaseItemDiscount { CouponId = "seat-item-coupon" }];
        schedule.Phases[1].Items[0].Discounts = [new SubscriptionSchedulePhaseItemDiscount { CouponId = "seat-item-coupon" }];
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(sourceSeat, 20)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Items.Single(i => i.Price == sourceSeat).Discounts
                    .Select(d => d.Coupon).SequenceEqual(new[] { "seat-item-coupon" }) &&
                opts.Phases[1].Items.Single(i => i.Price == targetSeat).Discounts
                    .Select(d => d.Coupon).SequenceEqual(new[] { "seat-item-coupon" })));
    }

    [Fact]
    public async Task Run_BusinessMigration_AllPhasesExpired_ReturnsConflict()
    {
        // The "no updatable phases" conflict is only reachable on the migration path.
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var sourceSeat = source.PasswordManager.StripeSeatPlanId;
        var subscription = CreateSubscription(items: [(sourceSeat, "si_1", 5)]);
        SetupGetSubscription(organization, subscription);

        var now = DateTime.UtcNow;
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = subscription.Id,
            Status = SubscriptionScheduleStatus.Active,
            EndBehavior = SubscriptionScheduleEndBehavior.Release,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = now.AddDays(-30),
                    EndDate = now.AddMinutes(-1),
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = sourceSeat, Quantity = 5 }],
                    Metadata = new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort-1" },
                    ProrationBehavior = ProrationBehavior.None
                }
            ]
        };

        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(sourceSeat, 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.IsT2);

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
    }

    [Fact]
    public async Task Run_BusinessMigration_PreservesPhaseMetadata()
    {
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var sourceSeat = source.PasswordManager.StripeSeatPlanId;
        var targetSeat = target.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(sourceSeat, "si_1", 5)]);
        SetupGetSubscription(organization, subscription);

        var metadata = new Dictionary<string, string>
        {
            [MetadataKeys.MigrationCohortId] = "foo",
            [MetadataKeys.MigrationCohortName] = "bar"
        };

        var schedule = CreateMockSchedule(subscription.Id, [(sourceSeat, 5)], [(targetSeat, 5)]);
        schedule.Phases[0].Metadata = metadata;
        schedule.Phases[1].Metadata = metadata;
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(sourceSeat, 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Metadata != null &&
                opts.Phases[0].Metadata[MetadataKeys.MigrationCohortId] == "foo" &&
                opts.Phases[0].Metadata[MetadataKeys.MigrationCohortName] == "bar" &&
                opts.Phases[1].Metadata != null &&
                opts.Phases[1].Metadata[MetadataKeys.MigrationCohortId] == "foo" &&
                opts.Phases[1].Metadata[MetadataKeys.MigrationCohortName] == "bar"));
    }

    [Fact]
    public async Task Run_BusinessMigration_PhaseMetadataNull_StaysNull()
    {
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var sourceSeat = source.PasswordManager.StripeSeatPlanId;
        var targetSeat = target.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(sourceSeat, "si_1", 5)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(subscription.Id, [(sourceSeat, 5)], [(targetSeat, 5)]);
        // Marker parked on the expired anchor so the assertion isolates per-phase preservation; production stamps every phase.
        schedule.Phases.Insert(0, new SubscriptionSchedulePhase
        {
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddMinutes(-5),
            Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_anchor", Quantity = 1 }],
            Metadata = new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort-1" },
            ProrationBehavior = ProrationBehavior.None
        });
        schedule.Phases[1].Metadata = null;
        schedule.Phases[2].Metadata = null;
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(sourceSeat, 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Metadata == null &&
                opts.Phases[1].Metadata == null));
    }

    [Fact]
    public async Task Run_BusinessMigration_PhaseMetadataEmpty_StaysEmpty()
    {
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var sourceSeat = source.PasswordManager.StripeSeatPlanId;
        var targetSeat = target.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(sourceSeat, "si_1", 5)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(subscription.Id, [(sourceSeat, 5)], [(targetSeat, 5)]);
        // Marker parked on the expired anchor so the assertion isolates per-phase preservation; production stamps every phase.
        schedule.Phases.Insert(0, new SubscriptionSchedulePhase
        {
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddMinutes(-5),
            Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_anchor", Quantity = 1 }],
            Metadata = new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort-1" },
            ProrationBehavior = ProrationBehavior.None
        });
        schedule.Phases[1].Metadata = new Dictionary<string, string>();
        schedule.Phases[2].Metadata = new Dictionary<string, string>();
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(sourceSeat, 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Metadata != null && opts.Phases[0].Metadata.Count == 0 &&
                opts.Phases[1].Metadata != null && opts.Phases[1].Metadata.Count == 0));
    }

    [Fact]
    public async Task Run_BusinessMigration_OnNormalized3PhaseSchedule_PreservesEverything()
    {
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var sourceSeat = source.PasswordManager.StripeSeatPlanId;
        var targetSeat = target.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(sourceSeat, "si_1", 10)]);
        SetupGetSubscription(organization, subscription);

        var now = DateTime.UtcNow;
        var cohortMetadata = new Dictionary<string, string>
        {
            [MetadataKeys.MigrationCohortId] = "cohort-1",
            [MetadataKeys.MigrationCohortName] = "ent-2020"
        };

        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = subscription.Id,
            Status = SubscriptionScheduleStatus.Active,
            EndBehavior = SubscriptionScheduleEndBehavior.Release,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = now.AddDays(-30),
                    EndDate = now.AddMinutes(-5),
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_anchor", Quantity = 1 }],
                    ProrationBehavior = ProrationBehavior.None
                },
                new SubscriptionSchedulePhase
                {
                    StartDate = now.AddMinutes(-5),
                    EndDate = now.AddYears(1),
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = sourceSeat, Quantity = 10 }],
                    Metadata = cohortMetadata,
                    ProrationBehavior = ProrationBehavior.None
                },
                new SubscriptionSchedulePhase
                {
                    StartDate = now.AddYears(1),
                    EndDate = now.AddYears(2),
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = targetSeat, Quantity = 10 }],
                    Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "five-percent-once" }],
                    Metadata = cohortMetadata,
                    ProrationBehavior = ProrationBehavior.None
                }
            ]
        };

        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(sourceSeat, 5)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases.Count == 2 &&
                opts.Phases[0].Metadata != null &&
                opts.Phases[0].Metadata[MetadataKeys.MigrationCohortId] == "cohort-1" &&
                opts.Phases[0].Items.Any(i => i.Price == sourceSeat && i.Quantity == 5) &&
                opts.Phases[1].Metadata != null &&
                opts.Phases[1].Metadata[MetadataKeys.MigrationCohortId] == "cohort-1" &&
                opts.Phases[1].Discounts != null &&
                opts.Phases[1].Discounts.Any(d => d.Coupon == "five-percent-once") &&
                opts.Phases[1].Items.Any(i => i.Price == targetSeat && i.Quantity == 5)));
    }

    [Fact]
    public async Task Run_NonMigration_AssignmentNull_UpdatesSubscriptionDirectly()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);
        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        _assignmentRepository.GetByOrganizationIdAsync(organization.Id)
            .Returns((OrganizationPlanMigrationCohortAssignment?)null);

        var schedule = CreateMockSchedule(subscription.Id, [("price_seats", 5)], [("price_seats_new", 5)]);
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Any(i => i.Price == "price_seats" && i.Quantity == 10)));
    }

    [Fact]
    public async Task Run_NonMigration_CohortMissingMigrationPathId_UpdatesSubscriptionDirectly()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);
        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = Guid.NewGuid()
        };
        _assignmentRepository.GetByOrganizationIdAsync(organization.Id).Returns(assignment);
        _cohortRepository.GetByIdAsync(assignment.CohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = assignment.CohortId,
            Name = "churn-only",
            MigrationPathId = null
        });

        var schedule = CreateMockSchedule(subscription.Id, [("price_seats", 5)], [("price_seats_new", 5)]);
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Any(i => i.Price == "price_seats" && i.Quantity == 10)));
    }

    [Fact]
    public async Task Run_ScheduleWithoutMigrationMetadata_DoesNotRewriteEvenWithCohortAssignment()
    {
        // PM-40537: a stale cohort assignment row must not resurrect the migration branch for a
        // schedule our code did not create; ownership is read from the schedule, not the org.
        var organization = CreateOrganization();

        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        // The migration path genuinely resolves (assignment, cohort, and both plans all stubbed),
        // which under the old rule was enough on its own to take the migration branch.
        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var subscription = CreateSubscription(items: [("2023-teams-org-seat-monthly", "si_1", 5)]);

        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_foreign",
            SubscriptionId = subscription.Id,
            Status = SubscriptionScheduleStatus.Active,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    EndDate = DateTime.UtcNow.AddMonths(1),
                    Metadata = new Dictionary<string, string> { ["negotiated_term"] = "3y" },
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = "2023-teams-org-seat-monthly", Quantity = 5 }]
                }
            ]
        };
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("2023-teams-org-seat-monthly", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionScheduleAsync(default!, default!);
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionUpdateOptions>());
    }

    [Fact]
    public async Task ResolvePhasePlansAsync_ValidPath_ReturnsDistinctSourceTargetPair()
    {
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);
        var sourceSeat = source.PasswordManager.StripeSeatPlanId;
        var targetSeat = target.PasswordManager.StripeSeatPlanId;

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var subscription = CreateSubscription(items: [(sourceSeat, "si_1", 5)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id,
            [(sourceSeat, 5)],
            [(targetSeat, 5)],
            phaseMetadata: new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort-1" });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(sourceSeat, 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        // Phase 1 uses source IDs; Phase 2 uses target IDs.
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Items.Any(i => i.Price == sourceSeat && i.Quantity == 10) &&
                opts.Phases[1].Items.Any(i => i.Price == targetSeat && i.Quantity == 10)));
    }

    [Fact]
    public async Task Run_AnnualUpgradeSchedule_AddSecretsManager_AddsAnnualPriceToPhase2()
    {
        var organization = CreateOrganization();
        organization.PlanType = PlanType.EnterpriseMonthly;

        var currentPlan = MockPlans.Get(PlanType.EnterpriseMonthly);
        var annualPlan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(annualPlan);

        var monthlySeat = currentPlan.PasswordManager.StripeSeatPlanId;
        var annualSeat = annualPlan.PasswordManager.StripeSeatPlanId;
        var monthlySmSeat = currentPlan.SecretsManager.StripeSeatPlanId;
        var annualSmSeat = annualPlan.SecretsManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(monthlySeat, "si_1", 5)]);
        SetupGetSubscription(organization, subscription);

        // No cohort assignment (default). The phase metadata marks this as an annual-upgrade schedule.
        var schedule = CreateMockSchedule(
            subscription.Id, [(monthlySeat, 5)], [(annualSeat, 5)],
            phaseMetadata: new Dictionary<string, string>
            {
                [MetadataKeys.AnnualUpgrade] = nameof(PlanType.EnterpriseMonthly)
            });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem(monthlySmSeat, 5)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases.Count == 2 &&
                opts.Phases[0].Items.Any(i => i.Price == monthlySmSeat && i.Quantity == 5) &&
                opts.Phases[1].Items.Any(i => i.Price == annualSmSeat && i.Quantity == 5) &&
                opts.Phases[1].Items.All(i => i.Price != monthlySmSeat)));
    }

    [Fact]
    public async Task Run_AnnualUpgradeSchedule_SeatUpdate_UpdatesAnnualQuantityInPhase2()
    {
        var organization = CreateOrganization();
        organization.PlanType = PlanType.EnterpriseMonthly;

        var currentPlan = MockPlans.Get(PlanType.EnterpriseMonthly);
        var annualPlan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(annualPlan);

        var monthlySeat = currentPlan.PasswordManager.StripeSeatPlanId;
        var annualSeat = annualPlan.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(monthlySeat, "si_1", 5)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id, [(monthlySeat, 5)], [(annualSeat, 5)],
            phaseMetadata: new Dictionary<string, string>
            {
                [MetadataKeys.AnnualUpgrade] = nameof(PlanType.EnterpriseMonthly)
            });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(monthlySeat, 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Items.Any(i => i.Price == monthlySeat && i.Quantity == 10) &&
                opts.Phases[1].Items.Any(i => i.Price == annualSeat && i.Quantity == 10) &&
                opts.Phases[1].Items.All(i => i.Price != monthlySeat)));
    }

    // PM-38333: the phase rewriter dropped item-level discounts, which strips them from the
    // annual phase and, because phase 1 is live, from the subscription itself.
    [Fact]
    public async Task Run_AnnualUpgradeSchedule_SeatUpdate_PreservesItemDiscountsOnBothPhases()
    {
        var organization = CreateOrganization();
        organization.PlanType = PlanType.EnterpriseMonthly;

        var currentPlan = MockPlans.Get(PlanType.EnterpriseMonthly);
        var annualPlan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(annualPlan);

        var monthlySeat = currentPlan.PasswordManager.StripeSeatPlanId;
        var annualSeat = annualPlan.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(monthlySeat, "si_1", 5)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id, [(monthlySeat, 5)], [(annualSeat, 5)],
            phaseMetadata: new Dictionary<string, string>
            {
                [MetadataKeys.AnnualUpgrade] = nameof(PlanType.EnterpriseMonthly)
            });
        schedule.Phases[0].Items[0].Discounts =
            [new SubscriptionSchedulePhaseItemDiscount { CouponId = "seat-coupon" }];
        schedule.Phases[1].Items[0].Discounts =
            [new SubscriptionSchedulePhaseItemDiscount { CouponId = "seat-coupon" }];
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(monthlySeat, 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Items.Single(i => i.Price == monthlySeat).Discounts
                    .Select(d => d.Coupon).SequenceEqual(new[] { "seat-coupon" }) &&
                opts.Phases[1].Items.Single(i => i.Price == annualSeat).Discounts
                    .Select(d => d.Coupon).SequenceEqual(new[] { "seat-coupon" })));
    }

    [Fact]
    public async Task Run_AnnualUpgradeSchedule_CarriesPhaseDiscountsByReuse_AndDoesNotMergeCustomerCoupon()
    {
        var organization = CreateOrganization();
        organization.PlanType = PlanType.EnterpriseMonthly;

        var currentPlan = MockPlans.Get(PlanType.EnterpriseMonthly);
        var annualPlan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(annualPlan);

        var monthlySeat = currentPlan.PasswordManager.StripeSeatPlanId;
        var annualSeat = annualPlan.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(monthlySeat, "si_1", 5)]);
        subscription.Customer = new Customer
        {
            Id = "cus_123",
            Discount = new Discount { Id = "di_customer", Source = new DiscountSource { CouponId = "customer-coupon", Coupon = new Coupon { Id = "customer-coupon" } } }
        };
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id, [(monthlySeat, 5)], [(annualSeat, 5)],
            phaseMetadata: new Dictionary<string, string>
            {
                [MetadataKeys.AnnualUpgrade] = nameof(PlanType.EnterpriseMonthly)
            });
        schedule.Phases[0].Discounts = [new SubscriptionSchedulePhaseDiscount { DiscountId = "di_own", CouponId = "coupon-own" }];
        schedule.Phases[1].Discounts = [new SubscriptionSchedulePhaseDiscount { DiscountId = "di_own", CouponId = "coupon-own" }];
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(monthlySeat, 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Discounts.Count == 1 &&
                opts.Phases[0].Discounts[0].Discount == "di_own" &&
                opts.Phases[0].Discounts[0].Coupon == null &&
                opts.Phases[1].Discounts.Count == 1 &&
                opts.Phases[1].Discounts[0].Discount == "di_own" &&
                opts.Phases[1].Discounts[0].Coupon == null &&
                opts.Phases.All(p => p.Discounts.All(d => d.Coupon != "customer-coupon" && d.Discount != "di_customer"))));
    }

    [Fact]
    public async Task Run_AnnualUpgradeSchedule_ChangeItemPrice_CarriesItemDiscountsOntoTheNewPrice()
    {
        var organization = CreateOrganization();
        organization.PlanType = PlanType.EnterpriseMonthly;

        var currentPlan = MockPlans.Get(PlanType.EnterpriseMonthly);
        var annualPlan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(annualPlan);

        var monthlySeat = currentPlan.PasswordManager.StripeSeatPlanId;
        var annualSeat = annualPlan.PasswordManager.StripeSeatPlanId;
        var replacementSeat = MockPlans.Get(PlanType.TeamsMonthly).PasswordManager.StripeSeatPlanId;

        Assert.NotEqual(monthlySeat, replacementSeat);

        var subscription = CreateSubscription(items: [(monthlySeat, "si_1", 10)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id, [(monthlySeat, 10)], [(annualSeat, 10)],
            phaseMetadata: new Dictionary<string, string>
            {
                [MetadataKeys.AnnualUpgrade] = nameof(PlanType.EnterpriseMonthly)
            });
        schedule.Phases[0].Items[0].Discounts =
            [new SubscriptionSchedulePhaseItemDiscount { CouponId = "seat-coupon" }];
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new ChangeItemPrice(monthlySeat, replacementSeat, 20)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Items.All(i => i.Price != monthlySeat) &&
                opts.Phases[0].Items.Single(i => i.Price == replacementSeat).Quantity == 20 &&
                opts.Phases[0].Items.Single(i => i.Price == replacementSeat).Discounts
                    .Select(d => d.Coupon).SequenceEqual(new[] { "seat-coupon" })));
    }

    [Fact]
    public async Task Run_AnnualUpgradeSchedule_ItemWithoutDiscounts_SendsNullNotAnEmptyList()
    {
        var organization = CreateOrganization();
        organization.PlanType = PlanType.EnterpriseMonthly;

        var currentPlan = MockPlans.Get(PlanType.EnterpriseMonthly);
        var annualPlan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(annualPlan);

        var monthlySeat = currentPlan.PasswordManager.StripeSeatPlanId;
        var annualSeat = annualPlan.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(monthlySeat, "si_1", 5)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id, [(monthlySeat, 5)], [(annualSeat, 5)],
            phaseMetadata: new Dictionary<string, string>
            {
                [MetadataKeys.AnnualUpgrade] = nameof(PlanType.EnterpriseMonthly)
            });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(monthlySeat, 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Items.All(i => i.Discounts == null) &&
                opts.Phases[1].Items.All(i => i.Discounts == null)));
    }

    [Fact]
    public async Task Run_AnnualUpgradeSchedule_StorageUpdate_UpdatesAnnualStorageInPhase2()
    {
        var organization = CreateOrganization();
        organization.PlanType = PlanType.EnterpriseMonthly;

        var currentPlan = MockPlans.Get(PlanType.EnterpriseMonthly);
        var annualPlan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(annualPlan);

        var monthlySeat = currentPlan.PasswordManager.StripeSeatPlanId;
        var annualSeat = annualPlan.PasswordManager.StripeSeatPlanId;
        var monthlyStorage = currentPlan.PasswordManager.StripeStoragePlanId;
        var annualStorage = annualPlan.PasswordManager.StripeStoragePlanId;

        var subscription = CreateSubscription(items: [(monthlySeat, "si_1", 5), (monthlyStorage, "si_2", 2)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id,
            [(monthlySeat, 5), (monthlyStorage, 2)],
            [(annualSeat, 5), (annualStorage, 2)],
            phaseMetadata: new Dictionary<string, string>
            {
                [MetadataKeys.AnnualUpgrade] = nameof(PlanType.EnterpriseMonthly)
            });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(monthlyStorage, 7)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Items.Any(i => i.Price == monthlyStorage && i.Quantity == 7) &&
                opts.Phases[1].Items.Any(i => i.Price == annualStorage && i.Quantity == 7) &&
                opts.Phases[1].Items.All(i => i.Price != monthlyStorage)));
    }

    [Fact]
    public async Task Run_AnnualUpgradeSchedule_ServiceAccountUpdate_UpdatesAnnualQuantityInPhase2()
    {
        var organization = CreateOrganization();
        organization.PlanType = PlanType.EnterpriseMonthly;

        var currentPlan = MockPlans.Get(PlanType.EnterpriseMonthly);
        var annualPlan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(annualPlan);

        var monthlySeat = currentPlan.PasswordManager.StripeSeatPlanId;
        var annualSeat = annualPlan.PasswordManager.StripeSeatPlanId;
        var monthlySa = currentPlan.SecretsManager.StripeServiceAccountPlanId;
        var annualSa = annualPlan.SecretsManager.StripeServiceAccountPlanId;

        var subscription = CreateSubscription(items: [(monthlySeat, "si_1", 5), (monthlySa, "si_2", 3)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(
            subscription.Id,
            [(monthlySeat, 5), (monthlySa, 3)],
            [(annualSeat, 5), (annualSa, 3)],
            phaseMetadata: new Dictionary<string, string>
            {
                [MetadataKeys.AnnualUpgrade] = nameof(PlanType.EnterpriseMonthly)
            });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(monthlySa, 8)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[0].Items.Any(i => i.Price == monthlySa && i.Quantity == 8) &&
                opts.Phases[1].Items.Any(i => i.Price == annualSa && i.Quantity == 8) &&
                opts.Phases[1].Items.All(i => i.Price != monthlySa)));
    }

    [Fact]
    public async Task Run_AnnualUpgradeSchedule_TakesPrecedenceOverCohortAssignment()
    {
        var organization = CreateOrganization();
        organization.PlanType = PlanType.EnterpriseMonthly;

        var currentPlan = MockPlans.Get(PlanType.EnterpriseMonthly);
        var cohortSource = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var annualLatest = MockPlans.Get(PlanType.EnterpriseAnnually);

        // Cohort migration path also present on this org.
        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, cohortSource,
            PlanType.EnterpriseAnnually, annualLatest);

        // Annual resolver also needs the current monthly plan.
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(currentPlan);

        var monthlySeat = currentPlan.PasswordManager.StripeSeatPlanId;
        var annualSeat = annualLatest.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(monthlySeat, "si_1", 5)]);
        SetupGetSubscription(organization, subscription);

        // Phase 1 is monthly, phase 2 carries the annual-latest seat price (annual-upgrade shape),
        // and the phase metadata marks this as an annual-upgrade schedule.
        var schedule = CreateMockSchedule(
            subscription.Id, [(monthlySeat, 5)], [(annualSeat, 5)],
            phaseMetadata: new Dictionary<string, string>
            {
                [MetadataKeys.AnnualUpgrade] = nameof(PlanType.EnterpriseMonthly)
            });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(monthlySeat, 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        // Annual resolution: monthly seat maps to the annual seat in phase 2, quantity updated, no
        // stray monthly seat. Cohort resolution would instead leave a monthly seat line in phase 2.
        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                opts.Phases[1].Items.Any(i => i.Price == annualSeat && i.Quantity == 10) &&
                opts.Phases[1].Items.All(i => i.Price != monthlySeat)));
    }

    [Fact]
    public async Task Run_ActiveScheduleNotAnnualUpgrade_NoCohort_LeavesScheduleUntouched()
    {
        // The org's plan type resolves an annual-latest target, but the active schedule carries no
        // annual-upgrade phase metadata marker, so it is not an annual-upgrade schedule and the
        // annual resolver returns null. With no cohort assignment, cohort resolution also returns
        // null, so the schedule is not a Bitwarden migration schedule: it is left untouched and the
        // subscription is updated directly (PM-40537).
        var organization = CreateOrganization();
        organization.PlanType = PlanType.EnterpriseMonthly;

        var currentPlan = MockPlans.Get(PlanType.EnterpriseMonthly);
        var annualLatest = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(annualLatest);

        var monthlySeat = currentPlan.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(monthlySeat, "si_1", 5)]);
        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        // Single monthly-only phase with no phase metadata at all, so annual-upgrade detection,
        // which reads the annual-upgrade metadata key rather than price content, returns false.
        var schedule = CreateMockSchedule(subscription.Id, [(monthlySeat, 5)]);
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(monthlySeat, 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Any(i => i.Price == monthlySeat && i.Quantity == 10)));
    }

    [Fact]
    public async Task Run_ScheduleCarryingAnnualSeatPriceWithoutMetadata_DoesNotTakeTheAnnualUpgradePath()
    {
        var organization = CreateOrganization();
        organization.PlanType = PlanType.EnterpriseMonthly;

        var currentPlan = MockPlans.Get(PlanType.EnterpriseMonthly);
        var annualPlan = MockPlans.Get(PlanType.EnterpriseAnnually);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseMonthly).Returns(currentPlan);
        _pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(annualPlan);

        var monthlySeat = currentPlan.PasswordManager.StripeSeatPlanId;
        var annualSeat = annualPlan.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(monthlySeat, "si_1", 5)]);
        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        // No metadata on either phase and no cohort assignment, so this is a schedule built outside
        // Bitwarden's code that happens to carry an annual price. It must fall through to the direct
        // subscription update rather than being rewritten.
        var schedule = CreateMockSchedule(subscription.Id, [(monthlySeat, 5)], [(annualSeat, 5)]);
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(monthlySeat, 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionScheduleAsync(default!, default!);
        await _stripeAdapter.ReceivedWithAnyArgs(1)
            .UpdateSubscriptionAsync(default!, default!);
    }

    [Fact]
    public async Task Run_NonMigration_SeatChange_TwoPhaseSchedule_LeavesScheduleUntouched()
    {
        // Bug 1 regression: a routine seat change must not rewrite the schedule's negotiated future phase.
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);
        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var schedule = CreateMockSchedule(subscription.Id, [("price_seats", 5)], [("price_seats", 15)]);
        schedule.Phases[1].Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "nego-10" }];
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 8)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Any(i => i.Price == "price_seats" && i.Quantity == 8)));
    }

    [Fact]
    public async Task Run_NonMigration_SinglePhaseWithCoupon_SeatChange_DoesNotTouchSchedule()
    {
        // Bug 2 regression: old code stripped the coupon on a lone remaining phase; the fix leaves it.
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);
        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var now = DateTime.UtcNow;
        var schedule = new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = subscription.Id,
            Status = SubscriptionScheduleStatus.Active,
            EndBehavior = SubscriptionScheduleEndBehavior.Release,
            Phases =
            [
                new SubscriptionSchedulePhase
                {
                    StartDate = now.AddDays(-30),
                    EndDate = now.AddDays(7),
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = "price_seats", Quantity = 5 }],
                    Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "nego-coupon" }],
                    ProrationBehavior = ProrationBehavior.None
                }
            ]
        };
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Any(i => i.Price == "price_seats" && i.Quantity == 10)));
    }

    [Fact]
    public async Task Run_NonMigration_AddItem_UpdatesSubscriptionDirectly()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);
        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var schedule = CreateMockSchedule(subscription.Id, [("price_seats", 5)], [("price_seats", 5)]);
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem("price_storage", 3)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Any(i => i.Price == "price_storage" && i.Quantity == 3)));
    }

    [Fact]
    public async Task Run_NonMigration_RemoveItem_UpdatesSubscriptionDirectly()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5), ("price_storage", "si_2", 2)]);
        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var schedule = CreateMockSchedule(subscription.Id, [("price_seats", 5)], [("price_seats", 5)]);
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new RemoveItem("price_storage")]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.Items.Any(i => i.Id == "si_2" && i.Deleted == true)));
    }

    [Fact]
    public async Task Run_NonMigration_AnnualNonStructural_WithSchedule_SetsPendingInvoiceItemInterval()
    {
        // Falling through with an active schedule still sets the monthly PendingInvoiceItemInterval.
        var organization = CreateOrganization();
        var subscription = CreateSubscription(
            status: SubscriptionStatus.Active,
            billingInterval: Intervals.Year,
            items: [("price_seats", "si_1", 5)]);
        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var schedule = CreateMockSchedule(subscription.Id, [("price_seats", 5)], [("price_seats", 5)]);
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(o =>
                o.PendingInvoiceItemInterval != null &&
                o.PendingInvoiceItemInterval.Interval == Intervals.Month));
    }

    [Fact]
    public async Task Run_NonMigration_SendInvoiceStructural_WithSchedule_FinalizesAndSends()
    {
        // Falling through with an active schedule still runs the send-invoice finalization.
        var organization = CreateOrganization();
        var subscription = CreateSubscription(
            collectionMethod: CollectionMethod.SendInvoice,
            items: [("price_seats", "si_1", 5)]);
        SetupGetSubscription(organization, subscription);

        var updatedSubscription = CreateSubscription(
            collectionMethod: CollectionMethod.SendInvoice,
            items: [("price_seats", "si_1", 5), ("price_storage", "si_2", 1)]);
        updatedSubscription.LatestInvoiceId = "inv_123";
        _stripeAdapter
            .UpdateSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionUpdateOptions>())
            .Returns(updatedSubscription);

        var draftInvoice = new Invoice { Id = "inv_123", Status = InvoiceStatus.Draft };
        _stripeAdapter.GetInvoiceAsync("inv_123", Arg.Any<InvoiceGetOptions>()).Returns(draftInvoice);
        var finalizedInvoice = new Invoice { Id = "inv_123", Status = InvoiceStatus.Open };
        _stripeAdapter
            .FinalizeInvoiceAsync("inv_123", Arg.Is<InvoiceFinalizeOptions>(o => o.AutoAdvance == false))
            .Returns(finalizedInvoice);

        var schedule = CreateMockSchedule(subscription.Id, [("price_seats", 5)], [("price_seats", 5)]);
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem("price_storage", 1)],
            ChargeImmediately = true
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.DidNotReceive().UpdateSubscriptionScheduleAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _stripeAdapter.Received(1).GetInvoiceAsync("inv_123", Arg.Any<InvoiceGetOptions>());
        await _stripeAdapter.Received(1).FinalizeInvoiceAsync("inv_123", Arg.Any<InvoiceFinalizeOptions>());
        await _stripeAdapter.Received(1).SendInvoiceAsync("inv_123");
    }

    [Fact]
    public async Task Run_Migration_SeatChange_RewritesSchedule()
    {
        var organization = CreateOrganization();
        var source = MockPlans.Get(PlanType.EnterpriseAnnually2020);
        var target = MockPlans.Get(PlanType.EnterpriseAnnually);

        SetupMigration(organization,
            MigrationPathId.Enterprise2020AnnualToCurrent,
            PlanType.EnterpriseAnnually2020, source,
            PlanType.EnterpriseAnnually, target);

        var sourceSeat = source.PasswordManager.StripeSeatPlanId;
        var targetSeat = target.PasswordManager.StripeSeatPlanId;

        var subscription = CreateSubscription(items: [(sourceSeat, "si_1", 5)]);
        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(subscription.Id, [(sourceSeat, 5)], [(targetSeat, 5)],
            phaseMetadata: new Dictionary<string, string> { [MetadataKeys.MigrationCohortId] = "cohort-1" });
        subscription.ScheduleId = schedule.Id;
        subscription.Schedule = schedule;

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity(sourceSeat, 8)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id, Arg.Any<SubscriptionScheduleUpdateOptions>());
        await _stripeAdapter.DidNotReceive().UpdateSubscriptionAsync(
            Arg.Any<string>(), Arg.Any<SubscriptionUpdateOptions>());
    }

    private void SetupMigration(
        Organization organization,
        MigrationPathId pathId,
        PlanType sourcePlanType,
        Bit.Core.Models.StaticStore.Plan sourcePlan,
        PlanType targetPlanType,
        Bit.Core.Models.StaticStore.Plan targetPlan)
    {
        var cohortId = Guid.NewGuid();
        _assignmentRepository.GetByOrganizationIdAsync(organization.Id).Returns(new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = cohortId
        });
        _cohortRepository.GetByIdAsync(cohortId).Returns(new OrganizationPlanMigrationCohort
        {
            Id = cohortId,
            Name = $"cohort-{pathId}",
            MigrationPathId = pathId,
            IsActive = true
        });
        _pricingClient.GetPlanOrThrow(sourcePlanType).Returns(sourcePlan);
        _pricingClient.GetPlanOrThrow(targetPlanType).Returns(targetPlan);
    }

    private static Organization CreateOrganization() => new()
    {
        Id = Guid.NewGuid(),
        GatewaySubscriptionId = "sub_123"
    };

    private static Subscription CreateSubscription(
        string status = SubscriptionStatus.Active,
        string collectionMethod = CollectionMethod.ChargeAutomatically,
        string billingInterval = Intervals.Month,
        Customer? customer = null,
        params (string priceId, string itemId, long quantity)[] items)
    {
        return new Subscription
        {
            Id = "sub_123",
            Status = status,
            CollectionMethod = collectionMethod,
            Customer = customer ?? new Customer
            {
                Id = "cus_123",
                Address = new Address { Country = "US" },
                TaxExempt = TaxExempt.None
            },
            Items = new StripeList<SubscriptionItem>
            {
                Data = items.Select(i => new SubscriptionItem
                {
                    Id = i.itemId,
                    Price = new Price
                    {
                        Id = i.priceId,
                        Recurring = new PriceRecurring { Interval = billingInterval }
                    },
                    Quantity = i.quantity
                }).ToList()
            }
        };
    }

    private void SetupGetSubscription(Organization organization, Subscription subscription)
    {
        _stripeAdapter
            .GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>())
            .Returns(subscription);
    }

    private void SetupUpdateSubscription(Subscription subscription)
    {
        _stripeAdapter
            .UpdateSubscriptionAsync(subscription.Id, Arg.Any<SubscriptionUpdateOptions>())
            .Returns(subscription);
    }

    private static SubscriptionSchedule CreateMockSchedule(
        string subscriptionId,
        (string priceId, long quantity)[] phase1Items,
        (string priceId, long quantity)[]? phase2Items = null,
        bool phase2Active = false,
        Dictionary<string, string>? phaseMetadata = null)
    {
        var phase1Start = phase2Active ? DateTime.UtcNow.AddYears(-1) : DateTime.UtcNow;
        var phase1End = phase2Active ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddYears(1);

        var phases = new List<SubscriptionSchedulePhase>
        {
            new()
            {
                StartDate = phase1Start,
                EndDate = phase1End,
                Items = phase1Items.Select(i =>
                    new SubscriptionSchedulePhaseItem { PriceId = i.priceId, Quantity = i.quantity }).ToList(),
                ProrationBehavior = ProrationBehavior.None,
                Metadata = phaseMetadata,
            }
        };

        if (phase2Items != null)
        {
            phases.Add(new SubscriptionSchedulePhase
            {
                StartDate = phase1End,
                EndDate = phase1End.AddYears(1),
                Items = phase2Items.Select(i =>
                    new SubscriptionSchedulePhaseItem { PriceId = i.priceId, Quantity = i.quantity }).ToList(),
                ProrationBehavior = ProrationBehavior.None,
                Metadata = phaseMetadata,
            });
        }

        return new SubscriptionSchedule
        {
            Id = "sub_sched_123",
            SubscriptionId = subscriptionId,
            Status = SubscriptionScheduleStatus.Active,
            EndBehavior = phase2Items != null
                ? SubscriptionScheduleEndBehavior.Release
                : SubscriptionScheduleEndBehavior.Cancel,
            Phases = phases
        };
    }

    // PM-37510 (T8): a caller-supplied subscription carrying an expanded Customer is reused, so the
    // command makes zero GetSubscriptionAsync calls of its own.
    [Fact]
    public async Task Run_SuppliedSubscriptionWithCustomer_DoesNotRefetch()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet, subscription);

        Assert.True(result.Success);
        await _stripeAdapter.DidNotReceiveWithAnyArgs()
            .GetSubscriptionAsync(default, default);
    }

    // PM-37510 (T8): a supplied subscription missing its expanded Customer is not safe to reuse, so
    // the command re-fetches exactly once.
    [Fact]
    public async Task Run_SuppliedSubscriptionWithoutCustomer_RefetchesOnce()
    {
        var organization = CreateOrganization();
        var suppliedWithoutCustomer = CreateSubscription(items: [("price_seats", "si_1", 5)]);
        suppliedWithoutCustomer.Customer = null;

        var refetched = CreateSubscription(items: [("price_seats", "si_1", 5)]);
        SetupGetSubscription(organization, refetched);
        SetupUpdateSubscription(refetched);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet, suppliedWithoutCustomer);

        Assert.True(result.Success);
        await _stripeAdapter.Received(1)
            .GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>());
    }

    // PM-41064: a supplied subscription whose discounts list was requested but not expanded
    // (Stripe.net deserializes it as a list of null entries) is not safe to reuse, so the command
    // self-heals by re-fetching rather than passing an unexpanded subscription to the discount builders.
    [Fact]
    public async Task Run_SuppliedSubscriptionWithUnexpandedDiscounts_RefetchesOnce()
    {
        var organization = CreateOrganization();
        var suppliedWithUnexpandedDiscounts = JsonConvert.DeserializeObject<Subscription>(
            """{"id":"sub_123","customer":{"id":"cus_123"},"discounts":["di_1"]}""")!;

        var refetched = CreateSubscription(items: [("price_seats", "si_1", 5)]);
        SetupGetSubscription(organization, refetched);
        SetupUpdateSubscription(refetched);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet, suppliedWithUnexpandedDiscounts);

        Assert.True(result.Success);
        await _stripeAdapter.Received(1)
            .GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>());
    }

    // PM-37510 (T8): with no supplied subscription the command fetches exactly once (existing default
    // behavior preserved).
    [Fact]
    public async Task Run_NoSuppliedSubscription_FetchesOnce()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);
        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);
        await _stripeAdapter.Received(1)
            .GetSubscriptionAsync(organization.GatewaySubscriptionId, Arg.Any<SubscriptionGetOptions>());
    }
}
