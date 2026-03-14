using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Commands;

using static StripeConstants;

public class UpdateOrganizationSubscriptionCommandTests
{
    private readonly IStripeAdapter _stripeAdapter = Substitute.For<IStripeAdapter>();
    private readonly UpdateOrganizationSubscriptionCommand _command;

    public UpdateOrganizationSubscriptionCommandTests()
    {
        _command = new UpdateOrganizationSubscriptionCommand(
            Substitute.For<ILogger<UpdateOrganizationSubscriptionCommand>>(),
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
    public async Task Run_StructuralChange_SetsAlwaysInvoiceProration()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        // AddItem is structural (IsStructural = !IsItemQuantityUpdate = true)
        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem("price_storage", 1)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.ProrationBehavior == ProrationBehavior.AlwaysInvoice));
    }

    [Fact]
    public async Task Run_NonStructuralChange_SetsCreateProrationsProration()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        // UpdateItemQuantity is non-structural
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
    public async Task Run_StructuralChange_ChargeAutomatically_SetsPendingIfIncomplete()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(
            collectionMethod: CollectionMethod.ChargeAutomatically,
            items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem("price_storage", 1)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.PaymentBehavior == PaymentBehavior.PendingIfIncomplete));
    }

    [Fact]
    public async Task Run_StructuralChange_SendInvoice_NoPaymentBehavior()
    {
        var organization = CreateOrganization();
        var subscription = CreateSubscription(
            collectionMethod: CollectionMethod.SendInvoice,
            items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new AddItem("price_storage", 1)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).UpdateSubscriptionAsync(subscription.Id,
            Arg.Is<SubscriptionUpdateOptions>(options =>
                options.PaymentBehavior == null));
    }

    [Fact]
    public async Task Run_NonStructuralChange_ChargeAutomatically_NoPaymentBehavior()
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
    public async Task Run_AnnualBilling_Structural_NoPendingInvoiceItemInterval()
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
            Changes = [new AddItem("price_storage", 1)]
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
            Changes = [new AddItem("price_storage", 1)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).GetInvoiceAsync("inv_123", Arg.Any<InvoiceGetOptions>());
        await _stripeAdapter.Received(1).FinalizeInvoiceAsync("inv_123", Arg.Any<InvoiceFinalizeOptions>());
        await _stripeAdapter.Received(1).SendInvoiceAsync("inv_123");
    }

    [Fact]
    public async Task Run_SendInvoice_Structural_NonDraftInvoice_DoesNotFinalizeOrSend()
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
            Changes = [new AddItem("price_storage", 1)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.Received(1).GetInvoiceAsync("inv_123", Arg.Any<InvoiceGetOptions>());
        await _stripeAdapter.DidNotReceive().FinalizeInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceFinalizeOptions>());
        await _stripeAdapter.DidNotReceive().SendInvoiceAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Run_ChargeAutomatically_Structural_DoesNotProcessInvoice()
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
            Changes = [new AddItem("price_storage", 1)]
        };

        var result = await _command.Run(organization, changeSet);

        Assert.True(result.Success);

        await _stripeAdapter.DidNotReceive().GetInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceGetOptions>());
        await _stripeAdapter.DidNotReceive().FinalizeInvoiceAsync(Arg.Any<string>(), Arg.Any<InvoiceFinalizeOptions>());
        await _stripeAdapter.DidNotReceive().SendInvoiceAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Run_SendInvoice_NonStructural_DoesNotProcessInvoice()
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
    public async Task Run_NonUSCustomer_NotReverseExempt_UpdatesTaxExemption()
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

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        await _command.Run(organization, changeSet);

        await _stripeAdapter.Received(1).UpdateCustomerAsync(customer.Id,
            Arg.Is<CustomerUpdateOptions>(options =>
                options.TaxExempt == TaxExempt.Reverse));
    }

    [Fact]
    public async Task Run_NonUSCustomer_AlreadyReverseExempt_DoesNotUpdateTaxExemption()
    {
        var customer = new Customer
        {
            Id = "cus_123",
            Address = new Address { Country = "DE" },
            TaxExempt = TaxExempt.Reverse
        };

        var organization = CreateOrganization();
        var subscription = CreateSubscription(customer: customer, items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        await _command.Run(organization, changeSet);

        await _stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }

    [Fact]
    public async Task Run_USCustomer_DoesNotUpdateTaxExemption()
    {
        var customer = new Customer
        {
            Id = "cus_123",
            Address = new Address { Country = "US" },
            TaxExempt = TaxExempt.None
        };

        var organization = CreateOrganization();
        var subscription = CreateSubscription(customer: customer, items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        await _command.Run(organization, changeSet);

        await _stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }

    [Fact]
    public async Task Run_SwissCustomer_WithNone_DoesNotUpdateTaxExemption()
    {
        var customer = new Customer
        {
            Id = "cus_123",
            Address = new Address { Country = "CH" },
            TaxExempt = TaxExempt.None
        };

        var organization = CreateOrganization();
        var subscription = CreateSubscription(customer: customer, items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        await _command.Run(organization, changeSet);

        await _stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }

    [Fact]
    public async Task Run_SwissCustomer_WithManualReverseSet_DoesNotUpdateTaxExemption()
    {
        var customer = new Customer
        {
            Id = "cus_123",
            Address = new Address { Country = "CH" },
            TaxExempt = TaxExempt.Reverse
        };

        var organization = CreateOrganization();
        var subscription = CreateSubscription(customer: customer, items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        await _command.Run(organization, changeSet);

        await _stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
    }

    [Fact]
    public async Task Run_CustomerWithNullAddress_DoesNotUpdateTaxExemption()
    {
        var customer = new Customer { Id = "cus_123", Address = null };

        var organization = CreateOrganization();
        var subscription = CreateSubscription(customer: customer, items: [("price_seats", "si_1", 5)]);

        SetupGetSubscription(organization, subscription);
        SetupUpdateSubscription(subscription);

        var changeSet = new OrganizationSubscriptionChangeSet
        {
            Changes = [new UpdateItemQuantity("price_seats", 10)]
        };

        await _command.Run(organization, changeSet);

        await _stripeAdapter.DidNotReceive().UpdateCustomerAsync(
            Arg.Any<string>(), Arg.Any<CustomerUpdateOptions>());
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

}
