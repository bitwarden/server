using System.Net;
using System.Text;
using System.Text.Json;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Api.AdminConsole.Endpoints;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Auth.Identity;
using Bit.HttpExtensions;
using Bit.OrganizationAuthorization;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OneOf.Types;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Endpoints;

public class OrganizationInviteLinkEndpointsTests
{
    private static readonly Guid _orgId = Guid.NewGuid();

    private const string _validCreateBody =
        """{"allowedDomains":["acme.com"],"invite":"invite","supportsConfirmation":false}""";

    private readonly ICreateOrganizationInviteLinkCommand _createCommand = Substitute.For<ICreateOrganizationInviteLinkCommand>();
    private readonly IGetOrganizationInviteLinkQuery _getQuery = Substitute.For<IGetOrganizationInviteLinkQuery>();
    private readonly IGetOrganizationInviteLinkStatusQuery _statusQuery = Substitute.For<IGetOrganizationInviteLinkStatusQuery>();
    private readonly IUpdateOrganizationInviteLinkCommand _updateCommand = Substitute.For<IUpdateOrganizationInviteLinkCommand>();
    private readonly IUpdateInviteSupportConfirmCommand _supportConfirmCommand = Substitute.For<IUpdateInviteSupportConfirmCommand>();
    private readonly IDeleteOrganizationInviteLinkCommand _deleteCommand = Substitute.For<IDeleteOrganizationInviteLinkCommand>();
    private readonly IRefreshOrganizationInviteLinkCommand _refreshCommand = Substitute.For<IRefreshOrganizationInviteLinkCommand>();
    private readonly IValidateOrganizationInviteLinkQuery _validateQuery = Substitute.For<IValidateOrganizationInviteLinkQuery>();
    private readonly IGetOrganizationInviteLinkPoliciesQuery _policiesQuery = Substitute.For<IGetOrganizationInviteLinkPoliciesQuery>();

    [Theory, BitAutoData]
    public async Task Create_WithValidBody_MapsToCommandRequestAndReturnsCreated(OrganizationInviteLink inviteLink)
    {
        PrepareForResponse(inviteLink);
        _createCommand.CreateAsync(Arg.Any<CreateOrganizationInviteLinkRequest>())
            .Returns(new CommandResult<OrganizationInviteLink>(inviteLink));

        var context = await InvokeAsync("OrganizationInviteLinks_Create", HttpMethods.Post, _validCreateBody);

        Assert.Equal((int)HttpStatusCode.Created, context.Response.StatusCode);
        Assert.Equal($"organizations/{_orgId}/invite-link", context.Response.Headers.Location.ToString());
        using var body = await ReadBodyAsync(context);
        Assert.Equal(inviteLink.Id, body.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("organizationInviteLink", body.RootElement.GetProperty("object").GetString());

        await _createCommand.Received(1).CreateAsync(Arg.Is<CreateOrganizationInviteLinkRequest>(r =>
            r.OrganizationId == _orgId &&
            r.AllowedDomains.SequenceEqual(new[] { "acme.com" }) &&
            r.Invite == "invite" &&
            !r.SupportsConfirmation));
    }

    [Fact]
    public async Task Create_WithInvalidBody_ReturnsValidationErrorsWithoutCallingCommand()
    {
        var context = await InvokeAsync(
            "OrganizationInviteLinks_Create",
            HttpMethods.Post,
            """{"allowedDomains":[],"invite":"invite","supportsConfirmation":false}""");

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        using var body = await ReadBodyAsync(context);
        Assert.True(body.RootElement.GetProperty("validationErrors").TryGetProperty("AllowedDomains", out _));
        await _createCommand.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory]
    [InlineData("""{"invite":5}""")]
    [InlineData("null")]
    [InlineData("")]
    public async Task Create_WithMalformedOrMissingBody_ReturnsBadRequestWithoutCallingCommand(string json)
    {
        var context = await InvokeAsync("OrganizationInviteLinks_Create", HttpMethods.Post, json);

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        await _createCommand.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Fact]
    public async Task Create_WhenLinkAlreadyExists_ReturnsConflict()
    {
        _createCommand.CreateAsync(Arg.Any<CreateOrganizationInviteLinkRequest>())
            .Returns(new CommandResult<OrganizationInviteLink>(new InviteLinkAlreadyExists()));

        var context = await InvokeAsync("OrganizationInviteLinks_Create", HttpMethods.Post, _validCreateBody);

        Assert.Equal((int)HttpStatusCode.Conflict, context.Response.StatusCode);
        Assert.Equal(new InviteLinkAlreadyExists().Message, await ReadErrorMessageAsync(context));
    }

    [Fact]
    public async Task Create_WhenCommandThrows_ReturnsErrorResponse()
    {
        _createCommand.CreateAsync(Arg.Any<CreateOrganizationInviteLinkRequest>())
            .Returns<CommandResult<OrganizationInviteLink>>(_ => throw new Core.Exceptions.NotFoundException());

        var context = await InvokeAsync("OrganizationInviteLinks_Create", HttpMethods.Post, _validCreateBody);

        Assert.Equal((int)HttpStatusCode.NotFound, context.Response.StatusCode);
        Assert.Equal("Resource not found.", await ReadErrorMessageAsync(context));
    }

    [Theory, BitAutoData]
    public async Task Get_WhenLinkExists_ReturnsOk(OrganizationInviteLink inviteLink)
    {
        PrepareForResponse(inviteLink);
        _getQuery.GetAsync(_orgId).Returns(new CommandResult<OrganizationInviteLink>(inviteLink));

        var context = await InvokeAsync("OrganizationInviteLinks_Get", HttpMethods.Get);

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        using var body = await ReadBodyAsync(context);
        Assert.Equal(inviteLink.Id, body.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Get_WhenNoLinkExists_ReturnsNotFound()
    {
        _getQuery.GetAsync(_orgId).Returns(new CommandResult<OrganizationInviteLink>(new InviteLinkNotFound()));

        var context = await InvokeAsync("OrganizationInviteLinks_Get", HttpMethods.Get);

        Assert.Equal((int)HttpStatusCode.NotFound, context.Response.StatusCode);
        Assert.Equal(new InviteLinkNotFound().Message, await ReadErrorMessageAsync(context));
    }

    [Fact]
    public async Task Get_WhenInviteLinkNotAvailable_ReturnsBadRequest()
    {
        _getQuery.GetAsync(_orgId).Returns(new CommandResult<OrganizationInviteLink>(new InviteLinkNotAvailable()));

        var context = await InvokeAsync("OrganizationInviteLinks_Get", HttpMethods.Get);

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task Update_WithValidBody_MapsToCommandRequestAndReturnsOk(OrganizationInviteLink inviteLink)
    {
        PrepareForResponse(inviteLink);
        _updateCommand.UpdateAsync(Arg.Any<UpdateOrganizationInviteLinkRequest>())
            .Returns(new CommandResult<OrganizationInviteLink>(inviteLink));

        var context = await InvokeAsync(
            "OrganizationInviteLinks_Update", HttpMethods.Put, """{"allowedDomains":["example.com"]}""");

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        await _updateCommand.Received(1).UpdateAsync(Arg.Is<UpdateOrganizationInviteLinkRequest>(r =>
            r.OrganizationId == _orgId && r.AllowedDomains.SequenceEqual(new[] { "example.com" })));
    }

    [Fact]
    public async Task Update_WithInvalidDomain_ReturnsValidationErrorsWithoutCallingCommand()
    {
        var context = await InvokeAsync(
            "OrganizationInviteLinks_Update", HttpMethods.Put, """{"allowedDomains":["not a domain"]}""");

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        await _updateCommand.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateInviteSupportConfirm_WithValidBody_MapsToCommandRequestAndReturnsOk(
        OrganizationInviteLink inviteLink)
    {
        PrepareForResponse(inviteLink);
        _supportConfirmCommand.UpdateAsync(Arg.Any<UpdateInviteSupportConfirmRequest>())
            .Returns(new CommandResult<OrganizationInviteLink>(inviteLink));

        var context = await InvokeAsync(
            "OrganizationInviteLinks_UpdateInviteSupportConfirm",
            HttpMethods.Put,
            """{"invite":"new-invite","supportsConfirmation":true}""");

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        await _supportConfirmCommand.Received(1).UpdateAsync(Arg.Is<UpdateInviteSupportConfirmRequest>(r =>
            r.OrganizationId == _orgId && r.Invite == "new-invite" && r.SupportsConfirmation));
    }

    [Fact]
    public async Task Delete_WhenLinkExists_ReturnsNoContent()
    {
        _deleteCommand.DeleteAsync(_orgId).Returns(new CommandResult(new None()));

        var context = await InvokeAsync("OrganizationInviteLinks_Delete", HttpMethods.Delete);

        Assert.Equal((int)HttpStatusCode.NoContent, context.Response.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenNoLinkExists_ReturnsNotFound()
    {
        _deleteCommand.DeleteAsync(_orgId).Returns(new CommandResult(new InviteLinkNotFound()));

        var context = await InvokeAsync("OrganizationInviteLinks_Delete", HttpMethods.Delete);

        Assert.Equal((int)HttpStatusCode.NotFound, context.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task Refresh_WithValidBody_MapsToCommandRequestAndReturnsOk(OrganizationInviteLink inviteLink)
    {
        PrepareForResponse(inviteLink);
        _refreshCommand.RefreshAsync(Arg.Any<RefreshOrganizationInviteLinkRequest>())
            .Returns(new CommandResult<OrganizationInviteLink>(inviteLink));

        var context = await InvokeAsync(
            "OrganizationInviteLinks_Refresh", HttpMethods.Post, """{"invite":"invite","supportsConfirmation":true}""");

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        await _refreshCommand.Received(1).RefreshAsync(Arg.Is<RefreshOrganizationInviteLinkRequest>(r =>
            r.OrganizationId == _orgId && r.Invite == "invite" && r.SupportsConfirmation));
    }

    [Theory, BitAutoData]
    public async Task GetStatus_WithValidBody_ReturnsStatus(OrganizationInviteLinkStatus status, Guid code)
    {
        _statusQuery.GetStatusAsync(_orgId, code).Returns(new CommandResult<OrganizationInviteLinkStatus>(status));

        var context = await InvokeAsync("OrganizationInviteLinks_GetStatus", HttpMethods.Post, CodeBody(code));

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        using var body = await ReadBodyAsync(context);
        Assert.Equal(status.OrganizationName, body.RootElement.GetProperty("organizationName").GetString());
        Assert.Equal(status.LinksEnabled, body.RootElement.GetProperty("linksEnabled").GetBoolean());
        Assert.Equal(status.SeatsAvailable, body.RootElement.GetProperty("seatsAvailable").GetBoolean());
        Assert.Equal(status.SupportsConfirmation, body.RootElement.GetProperty("supportsConfirmation").GetBoolean());
        Assert.Equal(status.Sso!.OrgSsoId, body.RootElement.GetProperty("sso").GetProperty("orgSsoId").GetString());
    }

    [Theory, BitAutoData]
    public async Task GetStatus_WhenNotFound_ReturnsNotFound(Guid code)
    {
        _statusQuery.GetStatusAsync(_orgId, code)
            .Returns(new CommandResult<OrganizationInviteLinkStatus>(new InviteLinkNotFound()));

        var context = await InvokeAsync("OrganizationInviteLinks_GetStatus", HttpMethods.Post, CodeBody(code));

        Assert.Equal((int)HttpStatusCode.NotFound, context.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task GetPolicies_WithValidBody_ReturnsList(List<Policy> policies, Guid code)
    {
        foreach (var policy in policies)
        {
            policy.Data = null;
        }

        _policiesQuery.GetPoliciesAsync(_orgId, code).Returns(new CommandResult<ICollection<Policy>>(policies));

        var context = await InvokeAsync("OrganizationInviteLinks_GetPolicies", HttpMethods.Post, CodeBody(code));

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        using var body = await ReadBodyAsync(context);
        Assert.Equal(policies.Count, body.RootElement.GetProperty("data").GetArrayLength());
    }

    [Theory, BitAutoData]
    public async Task ValidateEmailDomain_WhenAllowed_ReturnsIsAllowedTrue(Guid code)
    {
        _validateQuery.ValidateAsync(_orgId, code, "user@acme.com").Returns(new CommandResult(new None()));

        var context = await InvokeAsync(
            "OrganizationInviteLinks_ValidateEmailDomain", HttpMethods.Post, EmailBody(code, "user@acme.com"));

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        Assert.True(await ReadIsAllowedAsync(context));
    }

    [Theory, BitAutoData]
    public async Task ValidateEmailDomain_WhenDomainNotAllowed_ReturnsIsAllowedFalse(Guid code)
    {
        _validateQuery.ValidateAsync(_orgId, code, "user@other.com")
            .Returns(new CommandResult(new EmailDomainNotAllowed("Acme")));

        var context = await InvokeAsync(
            "OrganizationInviteLinks_ValidateEmailDomain", HttpMethods.Post, EmailBody(code, "user@other.com"));

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        Assert.False(await ReadIsAllowedAsync(context));
    }

    [Theory, BitAutoData]
    public async Task ValidateEmailDomain_WhenOtherError_ReturnsThatError(Guid code)
    {
        _validateQuery.ValidateAsync(_orgId, code, "user@acme.com")
            .Returns(new CommandResult(new InviteLinkNotFound()));

        var context = await InvokeAsync(
            "OrganizationInviteLinks_ValidateEmailDomain", HttpMethods.Post, EmailBody(code, "user@acme.com"));

        Assert.Equal((int)HttpStatusCode.NotFound, context.Response.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task ValidateEmailDomain_WithInvalidEmail_ReturnsValidationErrorsWithoutCallingQuery(Guid code)
    {
        var context = await InvokeAsync(
            "OrganizationInviteLinks_ValidateEmailDomain", HttpMethods.Post, EmailBody(code, "not-an-email"));

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        using var body = await ReadBodyAsync(context);
        Assert.True(body.RootElement.GetProperty("validationErrors").TryGetProperty("Email", out _));
        await _validateQuery.DidNotReceiveWithAnyArgs().ValidateAsync(default, default, default!);
    }

    [Theory]
    [InlineData("OrganizationInviteLinks_Get")]
    [InlineData("OrganizationInviteLinks_Create")]
    [InlineData("OrganizationInviteLinks_Update")]
    [InlineData("OrganizationInviteLinks_UpdateInviteSupportConfirm")]
    [InlineData("OrganizationInviteLinks_Delete")]
    [InlineData("OrganizationInviteLinks_Refresh")]
    public void ManagementEndpoints_RequireApplicationPolicyAndManageUsers(string endpointName)
    {
        var endpoint = FindEndpoint(BuildApp(), endpointName);

        Assert.Null(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), a => a.Policy == Policies.Application);
        Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            a => a is AuthorizeAttribute<ManageUsersRequirement>);
        Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()?.EndpointGroupName);
    }

    [Theory]
    [InlineData("OrganizationInviteLinks_GetStatus")]
    [InlineData("OrganizationInviteLinks_GetPolicies")]
    [InlineData("OrganizationInviteLinks_ValidateEmailDomain")]
    public void InviteeEndpoints_AllowAnonymous(string endpointName)
    {
        var endpoint = FindEndpoint(BuildApp(), endpointName);

        Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.DoesNotContain(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            a => a is AuthorizeAttribute<ManageUsersRequirement>);
    }

    [Theory]
    [InlineData("OrganizationInviteLinks_Get", 200, typeof(OrganizationInviteLinkResponseModel))]
    [InlineData("OrganizationInviteLinks_Create", 201, typeof(OrganizationInviteLinkResponseModel))]
    [InlineData("OrganizationInviteLinks_Update", 200, typeof(OrganizationInviteLinkResponseModel))]
    [InlineData("OrganizationInviteLinks_UpdateInviteSupportConfirm", 200, typeof(OrganizationInviteLinkResponseModel))]
    [InlineData("OrganizationInviteLinks_Refresh", 200, typeof(OrganizationInviteLinkResponseModel))]
    [InlineData("OrganizationInviteLinks_GetStatus", 200, typeof(OrganizationInviteLinkStatusResponseModel))]
    [InlineData("OrganizationInviteLinks_GetPolicies", 200, typeof(ListResponseModel<PolicyResponseModel>))]
    [InlineData("OrganizationInviteLinks_ValidateEmailDomain", 200, typeof(OrganizationInviteLinkValidateEmailDomainResponseModel))]
    public void OpenApi_DocumentsTheResponseModelRatherThanTheCommandResult(
        string endpointName, int statusCode, Type responseType)
    {
        var description = GetApiDescription(endpointName);

        var success = Assert.Single(description.SupportedResponseTypes, r => r.StatusCode == statusCode);
        Assert.Equal(responseType, success.Type);
        Assert.DoesNotContain(description.SupportedResponseTypes,
            r => r.Type is { IsGenericType: true } type && type.GetGenericTypeDefinition() == typeof(CommandResult<>));
        Assert.DoesNotContain(description.SupportedResponseTypes, r => r.Type == typeof(CommandResult));
    }

    [Fact]
    public void OpenApi_DeleteDocumentsNoContent()
    {
        var description = GetApiDescription("OrganizationInviteLinks_Delete");

        Assert.Contains(description.SupportedResponseTypes, r => r.StatusCode == StatusCodes.Status204NoContent);
        Assert.DoesNotContain(description.SupportedResponseTypes, r => r.StatusCode == StatusCodes.Status200OK);
    }

    private static void PrepareForResponse(OrganizationInviteLink inviteLink)
    {
        inviteLink.OrganizationId = _orgId;
        inviteLink.AllowedDomains = "[\"acme.com\"]";
        inviteLink.Code = Guid.NewGuid().ToString();
    }

    private static string CodeBody(Guid code) => $$"""{"organizationId":"{{_orgId}}","code":"{{code}}"}""";

    private static string EmailBody(Guid code, string email) =>
        $$"""{"organizationId":"{{_orgId}}","code":"{{code}}","email":"{{email}}"}""";

    private WebApplication BuildApp(Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(_createCommand);
        builder.Services.AddSingleton(_getQuery);
        builder.Services.AddSingleton(_statusQuery);
        builder.Services.AddSingleton(_updateCommand);
        builder.Services.AddSingleton(_supportConfirmCommand);
        builder.Services.AddSingleton(_deleteCommand);
        builder.Services.AddSingleton(_refreshCommand);
        builder.Services.AddSingleton(_validateQuery);
        builder.Services.AddSingleton(_policiesQuery);
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.MapGroup("/organizations/{orgId:guid}/invite-link").MapOrganizationInviteLinkEndpoints();
        app.MapGroup("/organizations/invite-link").MapAnonymousOrganizationInviteLinkEndpoints();
        return app;
    }

    private static RouteEndpoint FindEndpoint(IEndpointRouteBuilder app, string endpointName) =>
        app.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == endpointName);

    private ApiDescription GetApiDescription(string endpointName)
    {
        // ApiExplorer can't see an unstarted app's routes, so register them directly.
        WebApplication? app = null;
        app = BuildApp(services =>
        {
            services.AddEndpointsApiExplorer();
            services.AddSingleton<EndpointDataSource>(_ =>
                new CompositeEndpointDataSource(((IEndpointRouteBuilder)app!).DataSources));
        });

        return app.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Single(d => d.ActionDescriptor.EndpointMetadata
                .OfType<IEndpointNameMetadata>()
                .Any(name => name.EndpointName == endpointName));
    }

    private async Task<HttpContext> InvokeAsync(string endpointName, string method, string? json = null)
    {
        var app = BuildApp();
        var endpoint = FindEndpoint(app, endpointName);

        var scope = app.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Method = method;
        context.Request.RouteValues["orgId"] = _orgId.ToString();
        context.Response.Body = new MemoryStream();

        if (json is not null)
        {
            var body = Encoding.UTF8.GetBytes(json);
            context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature(canHaveBody: true));
            context.Request.ContentType = "application/json";
            context.Request.Body = new MemoryStream(body);
            context.Request.ContentLength = body.Length;
        }

        await endpoint.RequestDelegate!(context);
        return context;
    }

    private static async Task<JsonDocument> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body);
    }

    private static async Task<string?> ReadErrorMessageAsync(HttpContext context)
    {
        using var body = await ReadBodyAsync(context);
        return body.RootElement.GetProperty("message").GetString();
    }

    private static async Task<bool> ReadIsAllowedAsync(HttpContext context)
    {
        using var body = await ReadBodyAsync(context);
        return body.RootElement.GetProperty("isAllowed").GetBoolean();
    }

    private sealed class BodyDetectionFeature(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody { get; } = canHaveBody;
    }
}
