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
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [] });

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
    public async Task Run_BusinessMigration_CustomerDiscount_CarriedOntoFuturePhaseOnly()
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
                Discount = new Discount { Coupon = new Coupon { Id = "retention" } }
            },
            items: [(sourceSeat, "si_1", 5)]);

        SetupGetSubscription(organization, subscription);

        var schedule = CreateMockSchedule(subscription.Id, [(sourceSeat, 5)], [(targetSeat, 5)]);
        schedule.Phases[1].Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "migration-coupon" }];
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem(source.PasswordManager.StripeStoragePlanId, 3)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionScheduleAsync(
            schedule.Id,
            Arg.Is<SubscriptionScheduleUpdateOptions>(opts =>
                // Future phase: customer discount stacks with the existing migration coupon.
                opts.Phases[1].Discounts.Any(d => d.Coupon == "retention") &&
                opts.Phases[1].Discounts.Any(d => d.Coupon == "migration-coupon") &&
                // Active phase: customer discount NOT carried (would double-apply on the current period).
                (opts.Phases[0].Discounts == null || opts.Phases[0].Discounts.All(d => d.Coupon != "retention"))));
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
            [(target.PasswordManager.StripeSeatPlanId, 5)]);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
            [(target.PasswordManager.StripeSeatPlanId, 10)]);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
            [(targetSeat, 10)]);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
            [(targetSeat, 10)]);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
            [(targetSa, 5)]);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
            [(targetSeat, 5), (targetSa, 3)]);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
                    ProrationBehavior = ProrationBehavior.None
                },
                new SubscriptionSchedulePhase
                {
                    StartDate = now.AddMinutes(-5),
                    EndDate = now.AddDays(7),
                    Items = [new SubscriptionSchedulePhaseItem { PriceId = sourceSeat, Quantity = 10 }],
                    Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "migration-coupon" }],
                    ProrationBehavior = ProrationBehavior.None
                }
            ]
        };
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
                opts.Phases[0].Discounts.Any(d => d.Coupon == "migration-coupon")));
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
                    ProrationBehavior = ProrationBehavior.None
                }
            ]
        };

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
        schedule.Phases[0].Metadata = null;
        schedule.Phases[1].Metadata = null;
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
        schedule.Phases[0].Metadata = new Dictionary<string, string>();
        schedule.Phases[1].Metadata = new Dictionary<string, string>();
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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

        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
            [(targetSeat, 5)]);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
    public async Task Run_NonMigration_SeatChange_TwoPhaseSchedule_LeavesScheduleUntouched()
    {
        // Bug 1 regression: a routine seat change must not rewrite the schedule's negotiated future phase.
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);
        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var schedule = CreateMockSchedule(subscription.Id, [("price_seats", 5)], [("price_seats", 15)]);
        schedule.Phases[1].Discounts = [new SubscriptionSchedulePhaseDiscount { CouponId = "nego-10" }];
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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

        var schedule = CreateMockSchedule(subscription.Id, [(sourceSeat, 5)], [(targetSeat, 5)]);
        _stripeAdapter.ListSubscriptionSchedulesAsync(Arg.Any<SubscriptionScheduleListOptions>())
            .Returns(new StripeList<SubscriptionSchedule> { Data = [schedule] });

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
        bool phase2Active = false)
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
                ProrationBehavior = ProrationBehavior.None
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
                ProrationBehavior = ProrationBehavior.None
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
