using System.Net;
using System.Text;
using System.Text.Json;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Queries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test;

public class OrganizationPrivilegedControlsEndpointsRequestBindingTests
{
    private const string TrialMessage =
        "Work with your customer service representative to change your Privileged Controls seats.";

    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly FakeIsOnPrivilegedControlsSalesInitiatedTrialQuery _trialQuery = new();
    private readonly FakeChangePrivilegedControlsSeatsCommand _command = new();
    private readonly Guid _organizationId = Guid.NewGuid();

    public OrganizationPrivilegedControlsEndpointsRequestBindingTests() =>
        _organizationRepository.GetByIdAsync(_organizationId).Returns(new OrganizationEntity { Id = _organizationId });

    [Fact]
    public async Task ChangeSeats_SeatsAndLimit_BindsAndReturnsNoContent()
    {
        var context = await InvokeAsync("""{"seats":15,"maxAutoscaleSeats":20}""");

        Assert.Equal((int)HttpStatusCode.NoContent, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.True(_command.Called);
        Assert.Equal(15, _command.ReceivedSeats);
        Assert.Equal(20, _command.ReceivedMaxAutoscaleSeats);
    }

    [Fact]
    public async Task ChangeSeats_SeatsOnly_BindsNullLimitAndReturnsNoContent()
    {
        var context = await InvokeAsync("""{"seats":15}""");

        Assert.Equal((int)HttpStatusCode.NoContent, context.Response.StatusCode);
        Assert.True(_command.Called);
        Assert.Equal(15, _command.ReceivedSeats);
        Assert.Null(_command.ReceivedMaxAutoscaleSeats);
    }

    [Fact]
    public async Task ChangeSeats_MissingSeats_ForwardsZeroSoTheFactoryMessageReachesTheClient()
    {
        var context = await InvokeAsync("{}");

        Assert.Equal((int)HttpStatusCode.NoContent, context.Response.StatusCode);
        Assert.True(_command.Called);
        Assert.Equal(0, _command.ReceivedSeats);
    }

    [Fact]
    public async Task ChangeSeats_NonIntegerSeats_ReturnsBadRequestWithoutCallingCommand()
    {
        var context = await InvokeAsync("""{"seats":"ten"}""");

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Null(_trialQuery.ReceivedOrganization);
        Assert.False(_command.Called);
    }

    [Fact]
    public async Task ChangeSeats_CommandRejects_ReturnsBadRequestWithCommandMessage()
    {
        _command.Exception = new BadRequestException("Privileged Controls requires at least 10 seats.");

        var context = await InvokeAsync("""{"seats":5}""");

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Equal("Privileged Controls requires at least 10 seats.", await ReadErrorMessageAsync(context));
        Assert.Equal(5, _command.ReceivedSeats);
    }

    [Fact]
    public async Task ChangeSeats_SubscriptionMissingInStripe_ReturnsConflictWithMessageWithoutCallingCommand()
    {
        _trialQuery.Exception = new ConflictException(
            "Your organization's subscription is in an invalid state. Please contact support for assistance.");

        var context = await InvokeAsync("""{"seats":15}""");

        Assert.Equal((int)HttpStatusCode.Conflict, context.Response.StatusCode);
        Assert.Equal(
            "Your organization's subscription is in an invalid state. Please contact support for assistance.",
            await ReadErrorMessageAsync(context));
        Assert.False(_command.Called);
    }

    [Fact]
    public async Task ChangeSeats_SalesInitiatedTrial_ReturnsBadRequestWithTrialMessageWithoutCallingCommand()
    {
        _trialQuery.Result = true;

        var context = await InvokeAsync("""{"seats":15}""");

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Equal(TrialMessage, await ReadErrorMessageAsync(context));
        Assert.False(_command.Called);
    }

    private async Task<HttpContext> InvokeAsync(string json)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(_organizationRepository);
        builder.Services.AddSingleton<IIsOnPrivilegedControlsSalesInitiatedTrialQuery>(_trialQuery);
        builder.Services.AddSingleton<IChangePrivilegedControlsSeatsCommand>(_command);
        builder.Services.AddScoped<ChangePrivilegedControlsSeatsHandler>();
        var app = builder.Build();
        app.MapGroup("/{organizationId:guid}").MapOrganizationPrivilegedControlsEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "ChangePrivilegedControlsSeats");

        using var scope = app.Services.CreateScope();
        var body = Encoding.UTF8.GetBytes(json);
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature(canHaveBody: true));
        context.Request.Method = HttpMethods.Put;
        context.Request.RouteValues["organizationId"] = _organizationId.ToString();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(body);
        context.Request.ContentLength = body.Length;
        context.Response.Body = new MemoryStream();

        await endpoint.RequestDelegate!(context);
        return context;
    }

    private static async Task<string?> ReadErrorMessageAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.GetProperty("message").GetString();
    }

    private sealed class FakeChangePrivilegedControlsSeatsCommand : IChangePrivilegedControlsSeatsCommand
    {
        public Exception? Exception { get; set; }
        public bool Called { get; private set; }
        public int ReceivedSeats { get; private set; }
        public int? ReceivedMaxAutoscaleSeats { get; private set; }

        public Task Run(OrganizationEntity organization, int seats, int? maxAutoscaleSeats)
        {
            Called = true;
            ReceivedSeats = seats;
            ReceivedMaxAutoscaleSeats = maxAutoscaleSeats;
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }
    }

    private sealed class BodyDetectionFeature(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody { get; } = canHaveBody;
    }
}
