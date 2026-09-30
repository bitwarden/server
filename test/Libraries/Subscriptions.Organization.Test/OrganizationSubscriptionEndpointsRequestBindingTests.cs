using System.Net;
using System.Text;
using Bit.Core.Billing.Enums;
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
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

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

    private sealed class BodyDetectionFeature(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody { get; } = canHaveBody;
    }
}
