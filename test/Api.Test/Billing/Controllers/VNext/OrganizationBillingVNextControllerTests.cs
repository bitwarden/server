using Bit.Api.Billing.Controllers.VNext;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Organizations.PlanMigration.Commands;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Organizations.Queries;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Subscriptions.Commands;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using OneOf.Types;
using Stripe;
using Xunit;
using BadRequest = Bit.Core.Billing.Commands.BadRequest;
using Unhandled = Bit.Core.Billing.Commands.Unhandled;

namespace Bit.Api.Test.Billing.Controllers.VNext;

public class OrganizationBillingVNextControllerTests
{
    private readonly IGetChurnMitigationOfferQuery _getChurnMitigationOfferQuery;
    private readonly IRedeemChurnMitigationOfferCommand _redeemChurnMitigationOfferCommand;
    private readonly OrganizationBillingVNextController _sut;

    public OrganizationBillingVNextControllerTests()
    {
        _getChurnMitigationOfferQuery = Substitute.For<IGetChurnMitigationOfferQuery>();
        _redeemChurnMitigationOfferCommand = Substitute.For<IRedeemChurnMitigationOfferCommand>();

        _sut = new OrganizationBillingVNextController(
            Substitute.For<ICreateBitPayInvoiceForCreditCommand>(),
            Substitute.For<IGetBillingAddressQuery>(),
            _getChurnMitigationOfferQuery,
            Substitute.For<IGetCreditQuery>(),
            Substitute.For<IGetOrganizationMetadataQuery>(),
            Substitute.For<IGetOrganizationWarningsQuery>(),
            Substitute.For<IGetPaymentMethodQuery>(),
            _redeemChurnMitigationOfferCommand,
            Substitute.For<IRestartSubscriptionCommand>(),
            Substitute.For<IUpdateBillingAddressCommand>(),
            Substitute.For<IUpdatePaymentMethodCommand>());
    }

    [Theory, BitAutoData]
    public async Task GetChurnMitigationOfferAsync_EligibleOrg_ReturnsOkWithModel(Organization organization)
    {
        var offer = new ChurnMitigationOfferResult(
            CouponId: "churn-15-percent-once",
            PercentOff: 15m,
            AmountOff: null,
            Duration: "once",
            DurationInMonths: null,
            Name: "Churn 15% off");
        _getChurnMitigationOfferQuery.Run(organization).Returns(offer);

        var result = await _sut.GetChurnMitigationOfferAsync(organization);

        var okResult = Assert.IsType<Ok<ChurnMitigationOfferResult?>>(result);
        Assert.NotNull(okResult.Value);
        Assert.Equal("churn-15-percent-once", okResult.Value!.CouponId);
        Assert.Equal(15m, okResult.Value.PercentOff);
        Assert.Null(okResult.Value.AmountOff);
        Assert.Equal("once", okResult.Value.Duration);
        Assert.Null(okResult.Value.DurationInMonths);
        Assert.Equal("Churn 15% off", okResult.Value.Name);

        await _getChurnMitigationOfferQuery.Received(1).Run(organization);
    }

    [Theory, BitAutoData]
    public async Task GetChurnMitigationOfferAsync_IneligibleOrg_ReturnsOkWithNullValue(
        Organization organization)
    {
        // Ineligible orgs return Ok<T?> with Value == null and status 200. ASP.NET's
        // System.Text.Json path writes an empty response body for a top-level null
        // (not the literal `null` token); Angular's HttpClient surfaces that as null
        // to the web vault (PM-37173), which branches on `response === null`.
        _getChurnMitigationOfferQuery.Run(organization).Returns((ChurnMitigationOfferResult?)null);

        var result = await _sut.GetChurnMitigationOfferAsync(organization);

        var okResult = Assert.IsType<Ok<ChurnMitigationOfferResult?>>(result);
        Assert.Null(okResult.Value);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task RedeemChurnMitigationOfferAsync_RecheckFails_Returns400(Organization organization)
    {
        _redeemChurnMitigationOfferCommand.Run(organization)
            .Returns(new BillingCommandResult<None>(new BadRequest("Offer is no longer available.")));

        var result = await _sut.RedeemChurnMitigationOfferAsync(organization);

        Assert.IsType<BadRequest<Core.Models.Api.ErrorResponseModel>>(result);
        await _redeemChurnMitigationOfferCommand.Received(1).Run(organization);
    }

    [Theory, BitAutoData]
    public async Task RedeemChurnMitigationOfferAsync_StripeError_BubblesAsUnhandled(Organization organization)
    {
        // The underlying StripeException is wrapped into Unhandled by BaseBillingCommand.HandleAsync.
        // BaseBillingController.Handle maps Unhandled to a 500 JsonHttpResult<ErrorResponseModel>.
        var exception = new StripeException("internal")
        {
            StripeError = new StripeError { Code = "api_error", Message = "internal" }
        };
        _redeemChurnMitigationOfferCommand.Run(organization)
            .Returns(new BillingCommandResult<None>(new Unhandled(exception)));

        var result = await _sut.RedeemChurnMitigationOfferAsync(organization);

        var json = Assert.IsType<JsonHttpResult<Core.Models.Api.ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, json.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task RedeemChurnMitigationOfferAsync_HappyPath_ReturnsOk(Organization organization)
    {
        _redeemChurnMitigationOfferCommand.Run(organization)
            .Returns(new BillingCommandResult<None>(new None()));

        var result = await _sut.RedeemChurnMitigationOfferAsync(organization);

        Assert.IsType<Ok<None>>(result);
        await _redeemChurnMitigationOfferCommand.Received(1).Run(organization);
    }
}
