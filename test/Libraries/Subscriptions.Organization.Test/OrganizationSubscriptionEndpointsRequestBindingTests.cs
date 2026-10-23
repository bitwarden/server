using System.Net;
using System.Text;
using System.Text.Json;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Bit.Invoicing.InvoicePreviews.Models;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Models.Requests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Stripe;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;
using PlanFeatures = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Subscriptions.Organization.Test;

public class OrganizationSubscriptionEndpointsRequestBindingTests
{
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();

    [Fact]
    public async Task PlanChangePreview_BindsTierCadenceAndBillingAddressFromJsonBody()
    {
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(organizationId).Returns(new OrganizationEntity { Id = organizationId });
        var command = new FakePreviewOrganizationPlanChangeCommand { Result = SamplePreview() };

        var context = await InvokeAsync(command, organizationId,
            """{"tier":"enterprise","cadence":"monthly","billingAddress":{"country":"US","postalCode":"12345","taxId":{"code":"eu_vat","value":"DE123"}}}""");

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        Assert.Equal(PlanTierType.Enterprise, command.ReceivedRequest!.Tier);
        Assert.Equal(PlanCadenceType.Monthly, command.ReceivedRequest.Cadence);
        Assert.Equal("US", command.ReceivedRequest.BillingAddress!.Country);
        Assert.Equal("12345", command.ReceivedRequest.BillingAddress.PostalCode);
        Assert.Equal("eu_vat", command.ReceivedRequest.BillingAddress.TaxId!.Code);
        Assert.Equal("DE123", command.ReceivedRequest.BillingAddress.TaxId.Value);
    }

    [Fact]
    public async Task PlanChangePreview_MissingTier_ReturnsBadRequestWithoutCallingCommand()
    {
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(organizationId).Returns(new OrganizationEntity { Id = organizationId });
        var command = new FakePreviewOrganizationPlanChangeCommand { Result = SamplePreview() };

        // tier is a required member, so an omitted one fails binding with a 400 rather than defaulting silently.
        var context = await InvokeAsync(command, organizationId,
            """{"cadence":"monthly","billingAddress":{"country":"US","postalCode":"12345"}}""");

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Null(command.ReceivedRequest);
    }

    [Fact]
    public async Task PlanChangePreview_MissingCadence_ReturnsBadRequestWithoutCallingCommand()
    {
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(organizationId).Returns(new OrganizationEntity { Id = organizationId });
        var command = new FakePreviewOrganizationPlanChangeCommand { Result = SamplePreview() };

        var context = await InvokeAsync(command, organizationId,
            """{"tier":"enterprise","billingAddress":{"country":"US","postalCode":"12345"}}""");

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Null(command.ReceivedRequest);
    }

    [Fact]
    public async Task RestartPreview_BindsBillingAddressFromJsonBody()
    {
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(organizationId).Returns(new OrganizationEntity { Id = organizationId });
        var command = new FakePreviewOrganizationRestartCommand { Result = SamplePreview() };

        var context = await InvokeRestartAsync(command, organizationId,
            """{"billingAddress":{"country":"US","postalCode":"12345","taxId":{"code":"eu_vat","value":"DE123"}}}""");

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        Assert.Equal("US", command.ReceivedRequest!.BillingAddress!.Country);
        Assert.Equal("12345", command.ReceivedRequest.BillingAddress.PostalCode);
        Assert.Equal("eu_vat", command.ReceivedRequest.BillingAddress.TaxId!.Code);
        Assert.Equal("DE123", command.ReceivedRequest.BillingAddress.TaxId.Value);
    }

    [Fact]
    public async Task PurchasePrivilegedControls_BindsSeatsAndMaxAutoscaleSeatsFromJsonBody()
    {
        var command = new FakeAddPrivilegedControlsCommand();

        var context = await InvokePurchaseAsync(command, """{"seats":10,"maxAutoscaleSeats":20}""");

        Assert.Equal((int)HttpStatusCode.NoContent, context.Response.StatusCode);
        Assert.Equal(10, command.ReceivedSeats);
        Assert.Equal(20, command.ReceivedMaxAutoscaleSeats);
    }

    [Theory]
    [InlineData("""{"maxAutoscaleSeats":20}""", "Seats", "Privileged Controls seats must be between 1 and 100,000")]
    [InlineData("""{"seats":0}""", "Seats", "Privileged Controls seats must be between 1 and 100,000")]
    [InlineData("""{"seats":100001}""", "Seats", "Privileged Controls seats must be between 1 and 100,000")]
    [InlineData("""{"seats":10,"maxAutoscaleSeats":0}""", "MaxAutoscaleSeats", "The autoscale limit must be between 1 and 100,000")]
    [InlineData("""{"seats":10,"maxAutoscaleSeats":100001}""", "MaxAutoscaleSeats", "The autoscale limit must be between 1 and 100,000")]
    public async Task PurchasePrivilegedControls_MissingOrOutOfRangeValue_ReturnsBadRequestWithoutCallingCommand(
        string json, string expectedKey, string expectedMessage)
    {
        var command = new FakeAddPrivilegedControlsCommand();

        var context = await InvokePurchaseAsync(command, json);

        context.Response.Body.Position = 0;
        var error = await JsonSerializer.DeserializeAsync<ErrorResponseModel>(
            context.Response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Equal([expectedMessage], error!.ValidationErrors![expectedKey]);
        Assert.False(command.WasCalled);
    }

    [Fact]
    public async Task PurchasePrivilegedControls_SeatsAndAutoscaleLimitAtTheMaximum_CallsCommand()
    {
        var command = new FakeAddPrivilegedControlsCommand();

        var context = await InvokePurchaseAsync(
            command, $$"""{"seats":{{PlanConstants.MaxPurchasableQuantity}},"maxAutoscaleSeats":{{PlanConstants.MaxPurchasableQuantity}}}""");

        Assert.Equal((int)HttpStatusCode.NoContent, context.Response.StatusCode);
        Assert.Equal(PlanConstants.MaxPurchasableQuantity, command.ReceivedSeats);
        Assert.Equal(PlanConstants.MaxPurchasableQuantity, command.ReceivedMaxAutoscaleSeats);
    }

    [Fact]
    public async Task PurchasePrivilegedControls_CardError_ReturnsBadRequestWithStripeMessage()
    {
        var command = new FakeAddPrivilegedControlsCommand
        {
            ExceptionToThrow = new StripeException
            {
                StripeError = new StripeError { Type = StripeConstants.ErrorTypes.CardError, Message = "Your card was declined." }
            }
        };

        var context = await InvokePurchaseAsync(command, """{"seats":10}""");

        context.Response.Body.Position = 0;
        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Contains("Your card was declined.", await new StreamReader(context.Response.Body).ReadToEndAsync());
    }

    private async Task<HttpContext> InvokePurchaseAsync(FakeAddPrivilegedControlsCommand command, string json)
    {
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(organizationId).Returns(new OrganizationEntity
        {
            Id = organizationId,
            PlanType = PlanType.EnterpriseAnnually,
            GatewayCustomerId = "cus_123",
            GatewaySubscriptionId = "sub_123"
        });
        var pricingClient = Substitute.For<IPricingClient>();
        pricingClient.GetPlanOrThrow(PlanType.EnterpriseAnnually).Returns(new EnterpriseWithPrivilegedControlsPlan());

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(_organizationRepository);
        builder.Services.AddSingleton(pricingClient);
        builder.Services.AddSingleton(Substitute.For<IStripeAdapter>());
        builder.Services.AddSingleton<IAddPrivilegedControlsToExistingSubscriptionCommand>(command);
        // The library's own registration, so the handler is wired the way a host gets it.
        builder.Services.AddOrganizationSubscriptions();
        var app = builder.Build();
        app.MapGroup("/{organizationId:guid}").MapOrganizationSubscriptionEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText!.Contains("privileged-controls/purchase", StringComparison.Ordinal));

        using var scope = app.Services.CreateScope();
        var body = Encoding.UTF8.GetBytes(json);
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature(canHaveBody: true));
        context.Request.Method = HttpMethods.Post;
        context.Request.RouteValues["organizationId"] = organizationId.ToString();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(body);
        context.Request.ContentLength = body.Length;
        context.Response.Body = new MemoryStream();

        await endpoint.RequestDelegate!(context);
        return context;
    }

    private async Task<HttpContext> InvokeAsync(
        FakePreviewOrganizationPlanChangeCommand command, Guid organizationId, string json)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(_organizationRepository);
        builder.Services.AddSingleton<IPreviewOrganizationPlanChangeCommand>(command);
        builder.Services.AddScoped<PreviewOrganizationPlanChangeHandler>();
        var app = builder.Build();
        app.MapGroup("/{organizationId:guid}").MapOrganizationSubscriptionEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText!.Contains("plan-change/preview", StringComparison.Ordinal));

        using var scope = app.Services.CreateScope();
        var body = Encoding.UTF8.GetBytes(json);
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature(canHaveBody: true));
        context.Request.Method = HttpMethods.Post;
        context.Request.RouteValues["organizationId"] = organizationId.ToString();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(body);
        context.Request.ContentLength = body.Length;
        context.Response.Body = new MemoryStream();

        await endpoint.RequestDelegate!(context);
        return context;
    }

    private async Task<HttpContext> InvokeRestartAsync(
        FakePreviewOrganizationRestartCommand command, Guid organizationId, string json)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(_organizationRepository);
        builder.Services.AddSingleton<IPreviewOrganizationRestartCommand>(command);
        builder.Services.AddScoped<PreviewOrganizationRestartHandler>();
        var app = builder.Build();
        app.MapGroup("/{organizationId:guid}").MapOrganizationSubscriptionEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText!.Contains("restart/preview", StringComparison.Ordinal));

        using var scope = app.Services.CreateScope();
        var body = Encoding.UTF8.GetBytes(json);
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature(canHaveBody: true));
        context.Request.Method = HttpMethods.Post;
        context.Request.RouteValues["organizationId"] = organizationId.ToString();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(body);
        context.Request.ContentLength = body.Length;
        context.Response.Body = new MemoryStream();

        await endpoint.RequestDelegate!(context);
        return context;
    }

    private static InvoicePreview SamplePreview() => new()
    {
        PasswordManager = new PasswordManagerInvoiceItems
        {
            Seats = new InvoicePreviewItem { Reference = "pm-seat", Quantity = 1, Cost = 10m }
        },
        Cadence = PlanCadenceType.Annually,
        PlanTier = PlanTierType.Enterprise,
        EstimatedTax = 0m,
        Total = 10m,
        AmountDue = 10m
    };

    private sealed class FakePreviewOrganizationPlanChangeCommand : IPreviewOrganizationPlanChangeCommand
    {
        public PreviewOrganizationPlanChangeRequest? ReceivedRequest { get; private set; }
        public required InvoicePreview Result { get; init; }

        public Task<InvoicePreview> Run(OrganizationEntity organization, PreviewOrganizationPlanChangeRequest request)
        {
            ReceivedRequest = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakePreviewOrganizationRestartCommand : IPreviewOrganizationRestartCommand
    {
        public PreviewOrganizationRestartRequest? ReceivedRequest { get; private set; }
        public required InvoicePreview Result { get; init; }

        public Task<InvoicePreview> Run(OrganizationEntity organization, PreviewOrganizationRestartRequest request)
        {
            ReceivedRequest = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeAddPrivilegedControlsCommand : IAddPrivilegedControlsToExistingSubscriptionCommand
    {
        public bool WasCalled { get; private set; }
        public int ReceivedSeats { get; private set; }
        public int? ReceivedMaxAutoscaleSeats { get; private set; }
        public Exception? ExceptionToThrow { get; init; }

        public Task Run(OrganizationEntity organization, int seats, int? maxAutoscaleSeats)
        {
            WasCalled = true;
            ReceivedSeats = seats;
            ReceivedMaxAutoscaleSeats = maxAutoscaleSeats;
            return ExceptionToThrow is null ? Task.CompletedTask : Task.FromException(ExceptionToThrow);
        }
    }

    private sealed record EnterpriseWithPrivilegedControlsPlan : PlanFeatures
    {
        public EnterpriseWithPrivilegedControlsPlan()
        {
            ProductTier = ProductTierType.Enterprise;
            PrivilegedControls = new PrivilegedControlsPlanFeatures
            {
                StripeSeatPlanId = "privileged-controls-enterprise-seat-annually",
                DefaultSeatMinimum = 10
            };
        }
    }

    private sealed class BodyDetectionFeature(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody { get; } = canHaveBody;
    }
}
