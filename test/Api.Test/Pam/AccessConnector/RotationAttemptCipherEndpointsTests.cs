using System.Text.Json;
using Bit.Api.Pam.AccessConnector;
using Bit.Api.Vault.Models;
using Bit.Api.Vault.Models.Request;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Pam.AccessConnector.Commands;
using Bit.Core.Pam.AccessConnector.Queries;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
#if !OSS
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector;
using Bit.Services.Pam.Api.Endpoints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
#endif
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Pam.AccessConnector;

public class RotationAttemptCipherEndpointsTests
{
    private static readonly Guid _connectorId = Guid.NewGuid();
    private static readonly Guid _attemptId = Guid.NewGuid();
    private static readonly Guid _organizationId = Guid.NewGuid();
    private static readonly DateTime _revision = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A well-formed (AesCbc256_HmacSha256_B64) encrypted string; the content is opaque to the server.</summary>
    private static string Enc(string label) =>
        $"2.{Convert.ToBase64String(new byte[16])}|{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(label))}|" +
        Convert.ToBase64String(new byte[32]);

    private readonly ICurrentContext _currentContext = Substitute.For<ICurrentContext>();
    private readonly IGetRotationCipherQuery _query = Substitute.For<IGetRotationCipherQuery>();
    private readonly ISubmitCipherUpdateCommand _command = Substitute.For<ISubmitCipherUpdateCommand>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();

    public RotationAttemptCipherEndpointsTests()
    {
        _currentContext.PamAccessConnectorId.Returns(_connectorId);
    }

    private static Cipher ExistingLogin(string? key = "2.key|wrapped") => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = _organizationId,
        Type = CipherType.Login,
        Key = key,
        Data = "{\"Name\":\"2.old|name\",\"Password\":\"2.old|password\"}",
        RevisionDate = _revision,
    };

    private static CipherRequestModel ValidModel(Cipher existing) => new()
    {
        Type = CipherType.Login,
        OrganizationId = existing.OrganizationId.ToString(),
        Key = existing.Key,
        Name = Enc("name"),
        Login = new CipherLoginModel { Username = Enc("user"), Password = Enc("password") },
        LastKnownRevisionDate = _revision,
    };

    private void Reads(Cipher cipher) =>
        _query.GetAsync(_connectorId, _attemptId)
            .Returns(new RotationCipherAccess(cipher, FullCipherAccess.ForCipher(cipher.Id)));

    private Task<IResult> Put(CipherRequestModel model) =>
        RotationAttemptCipherEndpoints.PutCipherAsync(_attemptId, model, _currentContext, _query, _command);

    [Fact]
    public async Task GetCipher_ReturnsTheFullMiniResponse()
    {
        var cipher = ExistingLogin();
        Reads(cipher);
        _organizationRepository.GetByIdAsync(_organizationId)
            .Returns(new Organization { Id = _organizationId, UseTotp = true });

        var result = await RotationAttemptCipherEndpoints.GetCipherAsync(
            _attemptId, _currentContext, _query, _organizationRepository, new GlobalSettings());

        var response = Assert.IsType<FullCipherMiniResponseModel>(
            Assert.IsType<JsonHttpResult<FullCipherMiniResponseModel>>(result).Value);
        Assert.Equal(cipher.Id, response.Id);
        Assert.Equal(cipher.Data, response.Data);
        Assert.Equal(cipher.Key, response.Key);
        Assert.Equal(_revision, response.RevisionDate);
        Assert.True(response.OrganizationUseTotp);
    }

    [Fact]
    public async Task GetCipher_AttemptNotExecuting_PropagatesNotFound()
    {
        _query.GetAsync(_connectorId, _attemptId).Returns<RotationCipherAccess>(_ => throw new NotFoundException());

        await Assert.ThrowsAsync<NotFoundException>(() => RotationAttemptCipherEndpoints.GetCipherAsync(
            _attemptId, _currentContext, _query, _organizationRepository, new GlobalSettings()));
    }

    [Fact]
    public async Task PutCipher_BuildsDataFromTheStructuredFields_AndSubmitsIt()
    {
        var existing = ExistingLogin();
        Reads(existing);
        var model = ValidModel(existing);

        var result = await Put(model);

        Assert.IsType<Ok>(result);
        var call = Assert.Single(_command.ReceivedCalls());
        var args = call.GetArguments();
        Assert.Equal(_connectorId, args[0]);
        Assert.Equal(_attemptId, args[1]);
        Assert.Equal(_revision, args[3]);
        using var data = JsonDocument.Parse((string)args[2]!);
        Assert.Equal(Enc("password"), data.RootElement.GetProperty("Password").GetString());
        Assert.Equal(Enc("name"), data.RootElement.GetProperty("Name").GetString());
        Assert.Equal(Enc("user"), data.RootElement.GetProperty("Username").GetString());
    }

    [Fact]
    public async Task PutCipher_AttemptNotExecuting_PropagatesNotFoundWithoutWriting()
    {
        _query.GetAsync(_connectorId, _attemptId).Returns<RotationCipherAccess>(_ => throw new NotFoundException());

        await Assert.ThrowsAsync<NotFoundException>(() => Put(ValidModel(ExistingLogin())));

        await _command.DidNotReceiveWithAnyArgs().SubmitAsync(default, default, default!, default);
    }

    [Fact]
    public async Task PutCipher_CommandConflict_Propagates()
    {
        var existing = ExistingLogin();
        Reads(existing);
        _command.SubmitAsync(default, default, default!, default).ReturnsForAnyArgs<Task>(_ => throw new ConflictException());

        await Assert.ThrowsAsync<ConflictException>(() => Put(ValidModel(existing)));
    }

    [Fact]
    public async Task PutCipher_ExistingCipherNotALogin_Returns400()
    {
        var existing = ExistingLogin();
        existing.Type = CipherType.SecureNote;
        Reads(existing);

        await AssertRejected(ValidModel(existing));
    }

    [Fact]
    public async Task PutCipher_ModelTypeNotLogin_Returns400()
    {
        var existing = ExistingLogin();
        Reads(existing);
        var model = ValidModel(existing);
        model.Type = CipherType.Card;

        await AssertRejected(model);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task PutCipher_MissingOrUnparsableOrganization_Returns400(string? organizationId)
    {
        var existing = ExistingLogin();
        Reads(existing);
        var model = ValidModel(existing);
        model.OrganizationId = organizationId;

        await AssertRejected(model);
    }

    [Fact]
    public async Task PutCipher_DifferentOrganization_Returns400()
    {
        var existing = ExistingLogin();
        Reads(existing);
        var model = ValidModel(existing);
        model.OrganizationId = Guid.NewGuid().ToString();

        await AssertRejected(model);
    }

    [Fact]
    public async Task PutCipher_StoredCipherWithoutKey_Returns400()
    {
        var existing = ExistingLogin(key: null);
        Reads(existing);

        await AssertRejected(ValidModel(existing));
    }

    [Theory]
    [InlineData("2.other|key")]
    [InlineData("2.KEY|wrapped")]
    public async Task PutCipher_RequestKeyDiffersFromStored_Returns400(string key)
    {
        var existing = ExistingLogin();
        Reads(existing);
        var model = ValidModel(existing);
        model.Key = key;

        await AssertRejected(model);
    }

    [Fact]
    public async Task PutCipher_NullRequestKeyForKeyedCipher_Returns400()
    {
        var existing = ExistingLogin();
        Reads(existing);
        var model = ValidModel(existing);
        model.Key = null;

        await AssertRejected(model);
    }

    [Fact]
    public async Task PutCipher_IdenticalKeyWithAttachments_IsAccepted()
    {
        var existing = ExistingLogin();
        existing.SetAttachments(new Dictionary<string, CipherAttachment.MetaData>
        {
            ["a1"] = new CipherAttachment.MetaData { FileName = Enc("file"), Key = Enc("attachment key"), Size = 10 },
        });
        Reads(existing);

        Assert.IsType<Ok>(await Put(ValidModel(existing)));
    }

    [Fact]
    public async Task PutCipher_DataProvided_Returns400()
    {
        var existing = ExistingLogin();
        Reads(existing);
        var model = ValidModel(existing);
        model.Data = "{\"Password\":\"2.x|y\"}";

        await AssertRejected(model);
    }

    [Fact]
    public async Task PutCipher_MissingRevisionDate_Returns400()
    {
        var existing = ExistingLogin();
        Reads(existing);
        var model = ValidModel(existing);
        model.LastKnownRevisionDate = null;

        await AssertRejected(model);
    }

    private async Task AssertRejected(CipherRequestModel model)
    {
        await Assert.ThrowsAsync<BadRequestException>(() => Put(model));
        await _command.DidNotReceiveWithAnyArgs().SubmitAsync(default, default, default!, default);
    }

    [Fact]
    public void MapRotationAttemptCipherEndpoints_KeepsRouteNamesTagAndSchemas()
    {
        var builder = WebApplication.CreateSlimBuilder();
        // Unregistered parameter types would be inferred as the request body.
        builder.Services.AddSingleton(_currentContext);
        builder.Services.AddSingleton(_query);
        builder.Services.AddSingleton(_command);
        builder.Services.AddSingleton(_organizationRepository);
        builder.Services.AddSingleton<IGlobalSettings>(new GlobalSettings());
        var app = builder.Build();
        app.MapGroup("/access-connectors/rotation/attempts").MapRotationAttemptCipherEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(d => d.Endpoints).OfType<RouteEndpoint>().ToList();

        Assert.Equal(2, endpoints.Count);
        var get = Assert.Single(endpoints,
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ==
                 "Pam_AccessConnectors_Rotation_Attempts_GetCipher");
        var put = Assert.Single(endpoints,
            e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ==
                 "Pam_AccessConnectors_Rotation_Attempts_PutCipher");
        Assert.Equal("/access-connectors/rotation/attempts/{id:guid}/cipher", get.RoutePattern.RawText);
        Assert.Equal("/access-connectors/rotation/attempts/{id:guid}/cipher", put.RoutePattern.RawText);
        Assert.Contains("GET", get.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
        Assert.Contains("PUT", put.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
        Assert.All(endpoints, e => Assert.Contains("PamAccessConnectorRotationAttempts",
            e.Metadata.GetMetadata<ITagsMetadata>()!.Tags));

        // The shared schema, not a duplicate for the sealed Full* subclass.
        Assert.Contains(get.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(),
            p => p.StatusCode == StatusCodes.Status200OK && p.Type == typeof(CipherMiniResponseModel));
        Assert.Contains(put.Metadata.GetOrderedMetadata<IAcceptsMetadata>(),
            a => a.RequestType == typeof(CipherRequestModel));
    }

#if !OSS
    [Fact]
    public async Task MapRotationAttemptCipherEndpoints_WithMachineDefaults_GetsPolicyDocAndHeartbeat()
    {
        // Startup maps the group with the PAM machine defaults; the routes must carry the same policy, doc and
        // heartbeat as the rest of the machine surface.
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton(_currentContext);
        builder.Services.AddSingleton(_query);
        builder.Services.AddSingleton(_command);
        builder.Services.AddSingleton(_organizationRepository);
        builder.Services.AddSingleton<IGlobalSettings>(new GlobalSettings());
        var app = builder.Build();
        app.MapGroup("/access-connectors/rotation/attempts")
            .WithPamAccessConnectorMachineDefaults()
            .MapRotationAttemptCipherEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(d => d.Endpoints).OfType<RouteEndpoint>().ToList();

        Assert.Equal(2, endpoints.Count);
        foreach (var endpoint in endpoints)
        {
            Assert.Equal("internal", endpoint.Metadata.GetMetadata<IEndpointGroupNameMetadata>()?.EndpointGroupName);
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == Policies.AccessConnector);

            // Without a PamAccessConnectorId the heartbeat filter 404s ahead of the handler.
            var services = new ServiceCollection();
            services.AddLogging();
            var hostEnvironment = Substitute.For<IHostEnvironment>();
            hostEnvironment.EnvironmentName.Returns(Environments.Production);
            services.AddSingleton(hostEnvironment);
            services.AddSingleton(_currentContext);
            services.AddSingleton(Substitute.For<IPamAccessConnectorRepository>());
            services.AddSingleton(Options.Create(new PamRotationOptions()));
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(_query);
            services.AddSingleton(_command);
            services.AddSingleton(_organizationRepository);
            services.AddSingleton<IGlobalSettings>(new GlobalSettings());
            _currentContext.PamAccessConnectorId.Returns((Guid?)null);
            var httpContext = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider(),
                Response = { Body = new MemoryStream() },
            };

            await endpoint.RequestDelegate!(httpContext);

            Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        }
    }
#endif
}
