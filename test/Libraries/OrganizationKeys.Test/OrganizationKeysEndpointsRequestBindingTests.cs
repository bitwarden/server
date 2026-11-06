using System.Net;
using System.Text;
using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;
using Bit.CurrentUser;
using Bit.OrganizationKeys.Commands;
using Bit.OrganizationKeys.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.OrganizationKeys.Test;

/// <summary>
/// Runs the mapped endpoint's request delegate, so binding, the endpoint's validation filter, and the group's
/// exception handling run together. Authorization is middleware and does not run here.
/// </summary>
public class OrganizationKeysEndpointsRequestBindingTests
{
    private const string _organizationKeyId = "0123456789abcdef0123456789abcdef";

    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly ICurrentUserContext _currentUserContext = Substitute.For<ICurrentUserContext>();
    private readonly IOrganizationUserRepository _organizationUserRepository =
        Substitute.For<IOrganizationUserRepository>();
    private readonly FakeSetOrganizationKeyIdCommand _command = new();

    public OrganizationKeysEndpointsRequestBindingTests()
    {
        _currentUserContext.UserId.Returns(_userId);
        _organizationUserRepository.GetByOrganizationAsync(_organizationId, _userId)
            .Returns(new OrganizationUser { OrganizationId = _organizationId, UserId = _userId, Key = "2.wrapped|key" });
    }

    [Fact]
    public async Task SetOrganizationKeyId_ValidKeyId_BindsAndReturnsOk()
    {
        var context = await InvokeAsync($$"""{"organizationKeyId":"{{_organizationKeyId}}"}""");

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        Assert.True(_command.Called);
        Assert.Equal(_organizationId, _command.ReceivedOrganizationId);
        Assert.Equal(KeyId.FromHexEncodedString(_organizationKeyId), _command.ReceivedOrganizationKeyId);
    }

    [Fact]
    public async Task SetOrganizationKeyId_MalformedKeyId_ReturnsBadRequestWithModelStateErrorWithoutReadingMembershipOrCallingCommand()
    {
        var context = await InvokeAsync("""{"organizationKeyId":"not-a-key-id"}""");

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        using var document = await ReadBodyAsync(context);
        Assert.Equal("The model state is invalid.", document.RootElement.GetProperty("message").GetString());
        Assert.True(document.RootElement.GetProperty("validationErrors")
            .TryGetProperty("OrganizationKeyId", out _));
        await _organizationUserRepository.DidNotReceiveWithAnyArgs().GetByOrganizationAsync(default, default);
        Assert.False(_command.Called);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"organizationKeyId":null}""")]
    [InlineData("not json")]
    public async Task SetOrganizationKeyId_MissingOrUnreadableBody_ReturnsBadRequestWithoutCallingCommand(string json)
    {
        var context = await InvokeAsync(json);

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.False(_command.Called);
    }

    [Fact]
    public async Task SetOrganizationKeyId_CommandConflicts_ReturnsConflictWithCommandMessage()
    {
        _command.Exception = new ConflictException("Organization key id is already set.");

        var context = await InvokeAsync($$"""{"organizationKeyId":"{{_organizationKeyId}}"}""");

        Assert.Equal((int)HttpStatusCode.Conflict, context.Response.StatusCode);
        using var document = await ReadBodyAsync(context);
        Assert.Equal("Organization key id is already set.", document.RootElement.GetProperty("message").GetString());
    }

    private async Task<HttpContext> InvokeAsync(string json)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(_currentUserContext);
        builder.Services.AddSingleton(_organizationUserRepository);
        builder.Services.AddSingleton<ISetOrganizationKeyIdCommand>(_command);
        builder.Services.AddScoped<SetOrganizationKeyIdHandler>();
        var app = builder.Build();
        app.MapGroup("/{orgId:guid}").MapOrganizationKeysEndpoints();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "SetOrganizationKeyId");

        using var scope = app.Services.CreateScope();
        var body = Encoding.UTF8.GetBytes(json);
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetectionFeature(canHaveBody: true));
        context.Request.Method = HttpMethods.Post;
        context.Request.RouteValues["orgId"] = _organizationId.ToString();
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(body);
        context.Request.ContentLength = body.Length;
        context.Response.Body = new MemoryStream();

        await endpoint.RequestDelegate!(context);
        return context;
    }

    private static async Task<JsonDocument> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body);
    }

    private sealed class BodyDetectionFeature(bool canHaveBody) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody { get; } = canHaveBody;
    }
}
