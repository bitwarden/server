using System.Security.Claims;
using System.Text.Json;
using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Dirt.Enums;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Tools.Services;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Identity.Controllers;
using Bit.Identity.IdentityServer.ClientProviders;
using Bit.Identity.Models.Request.Accounts;
using Bit.Identity.Models.Response.Accounts;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Identity.Test.Controllers;

public class StaleCopyDeletionControllerTests : IDisposable
{
    private const string Token = "a-token";
    private const int InternalPort = 33657;
    private const string Client = $"{StaleCopyDeletionClientProvider.Prefix}.internal-service";
    private const string WithAttachment = """{"an-attachment":{"FileName":"2.encrypted-name","Key":"2.encrypted-key","Size":1}}""";

    private readonly IProviderRepository _providerRepository = Substitute.For<IProviderRepository>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICipherRepository _cipherRepository = Substitute.For<ICipherRepository>();
    private readonly IAttachmentStorageService _attachmentStorageService = Substitute.For<IAttachmentStorageService>();
    private readonly ISendFileStorageService _sendFileStorageService = Substitute.For<ISendFileStorageService>();
    private readonly IOrganizationAbilityCacheService _abilityCache = Substitute.For<IOrganizationAbilityCacheService>();
    private readonly IProviderAbilityCacheService _providerAbilityCache = Substitute.For<IProviderAbilityCacheService>();
    private readonly ITokenValidator _tokenValidator = Substitute.For<ITokenValidator>();
    private readonly GlobalSettings _globalSettings = Substitute.For<GlobalSettings>();
    private readonly ILogger<StaleCopyDeletionController> _logger = Substitute.For<ILogger<StaleCopyDeletionController>>();
    private readonly StaleCopyDeletionController _sut;

    public StaleCopyDeletionControllerTests()
    {
        _globalSettings.StaleCopyDeletionClientSecret.Returns("a-secret");
        _globalSettings.StaleCopyDeletionPort.Returns(InternalPort);
        _providerRepository.GetByIdAsync(default).ReturnsNullForAnyArgs();
        _organizationRepository.GetByIdAsync(default).ReturnsNullForAnyArgs();
        _userRepository.GetByIdAsync(default).ReturnsNullForAnyArgs();
        _cipherRepository.GetManyByOrganizationIdAsync(default).ReturnsForAnyArgs([]);
        _cipherRepository.GetManyByUserIdAsync(default, default).ReturnsForAnyArgs([]);
        _sut = new StaleCopyDeletionController(_providerRepository, _organizationRepository, _userRepository,
            _cipherRepository, _attachmentStorageService, _sendFileStorageService, _abilityCache, _providerAbilityCache, _tokenValidator,
            _globalSettings, _logger)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        _sut.HttpContext.Connection.LocalPort = InternalPort;
        Bearer(Token);
        Validates(Token, Client);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task Deletes_providers_then_organizations_then_users_with_their_files_and_answers_the_counts()
    {
        var provider = new Provider { Id = Guid.NewGuid() };
        var organization = new Organization { Id = Guid.NewGuid() };
        var user = new User { Id = Guid.NewGuid() };
        var gone = Guid.NewGuid();
        _providerRepository.GetByIdAsync(provider.Id).Returns(provider);
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        _userRepository.GetByIdAsync(user.Id).Returns(user);
        var organizationCiphers = new List<Cipher>
        {
            new() { Id = Guid.NewGuid(), OrganizationId = organization.Id, Attachments = WithAttachment },
            new() { Id = Guid.NewGuid(), OrganizationId = organization.Id },
        };
        _cipherRepository.GetManyByOrganizationIdAsync(organization.Id).Returns(organizationCiphers);
        var userCiphers = new List<CipherDetails> { new() { Id = Guid.NewGuid(), UserId = user.Id, Attachments = WithAttachment } };
        _cipherRepository.GetManyByUserIdAsync(user.Id, false).Returns(userCiphers);

        var result = await _sut.DeleteAsync(new StaleCopyDeletionRequestModel
        {
            ProviderIds = [provider.Id],
            OrganizationIds = [organization.Id, gone, organization.Id],
            UserIds = [user.Id, user.Id],
        });

        var answer = Answer(result);
        Assert.Equal(1, answer.ProvidersDeleted);
        Assert.Equal(1, answer.OrganizationsDeleted);
        Assert.Equal(1, answer.UsersDeleted);
        Assert.Equal(1, answer.AlreadyGone);
        Assert.Equal(2, answer.CiphersWithAttachmentsDeleted);
        Assert.Equal("staleCopyDeletion", answer.Object);
        Received.InOrder(() =>
        {
            _providerRepository.DeleteAsync(provider);
            _providerAbilityCache.DeleteProviderAbilityAsync(provider.Id);
            _attachmentStorageService.DeleteAttachmentsForCipherAsync(organizationCiphers[0].Id);
            _sendFileStorageService.DeleteFilesForOrganizationAsync(organization.Id);
            _organizationRepository.DeleteAndCreateDeleteTasksAsync(organization,
                Arg.Is<IEnumerable<OrganizationDeleteTaskType>>(tasks => tasks.Single() == OrganizationDeleteTaskType.EventsCleanup));
            _abilityCache.DeleteOrganizationAbilityAsync(organization.Id, Arg.Any<CancellationToken>());
            _attachmentStorageService.DeleteAttachmentsForCipherAsync(userCiphers[0].Id);
            _sendFileStorageService.DeleteFilesForUserAsync(user.Id);
            _userRepository.DeleteAsync(user);
        });
        await _attachmentStorageService.DidNotReceive().DeleteAttachmentsForCipherAsync(organizationCiphers[1].Id);
        await _organizationRepository.Received(1).DeleteAndCreateDeleteTasksAsync(Arg.Any<Organization>(), Arg.Any<IEnumerable<OrganizationDeleteTaskType>>());
        await _userRepository.Received(1).DeleteAsync(Arg.Any<User>());
        // As the provider's own deletion, its cached ability goes with it.
        await _providerAbilityCache.Received(1).DeleteProviderAbilityAsync(Arg.Any<Guid>());
        // Its own repository delete, never the plain one that leaves the event log behind.
        await _organizationRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Fact]
    public async Task Answers_its_counts_in_camel_case()
    {
        var result = await _sut.DeleteAsync(new StaleCopyDeletionRequestModel { UserIds = [Guid.NewGuid()] });

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value,
            typeof(StaleCopyDeletionResponseModel), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(0, json.RootElement.GetProperty("providersDeleted").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("organizationsDeleted").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("usersDeleted").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("alreadyGone").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("ciphersWithAttachmentsDeleted").GetInt32());
    }

    [Fact]
    public async Task Is_safe_to_repeat_counting_what_is_gone()
    {
        var organization = new Organization { Id = Guid.NewGuid() };
        var user = new User { Id = Guid.NewGuid() };
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization, (Organization?)null);
        _userRepository.GetByIdAsync(user.Id).Returns(user, (User?)null);
        var request = new StaleCopyDeletionRequestModel { OrganizationIds = [organization.Id], UserIds = [user.Id] };

        await _sut.DeleteAsync(request);
        _organizationRepository.ClearReceivedCalls();
        _userRepository.ClearReceivedCalls();
        _sendFileStorageService.ClearReceivedCalls();
        var again = Answer(await _sut.DeleteAsync(request));

        Assert.Equal(0, again.OrganizationsDeleted);
        Assert.Equal(0, again.UsersDeleted);
        Assert.Equal(2, again.AlreadyGone);
        await _organizationRepository.DidNotReceiveWithAnyArgs().DeleteAndCreateDeleteTasksAsync(default!, default!);
        await _userRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
        await _sendFileStorageService.DidNotReceiveWithAnyArgs().DeleteFilesForUserAsync(default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Does_nothing_for_empty_or_absent_lists(bool absent)
    {
        var request = absent
            ? new StaleCopyDeletionRequestModel()
            : new StaleCopyDeletionRequestModel { ProviderIds = [], OrganizationIds = [], UserIds = [] };

        var answer = Answer(await _sut.DeleteAsync(request));

        Assert.Equal(0, answer.AlreadyGone);
        await AssertNothingTouchedAsync();
    }

    [Theory]
    [InlineData(101, 0, 0)]
    [InlineData(0, 101, 0)]
    [InlineData(0, 0, 101)]
    [InlineData(1, 50, 50)]
    public async Task Refuses_more_than_a_hundred_ids_deleting_nothing(int providers, int organizations, int users)
    {
        var request = new StaleCopyDeletionRequestModel
        {
            ProviderIds = Ids(providers),
            OrganizationIds = Ids(organizations),
            UserIds = Ids(users),
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeleteAsync(request));

        await AssertNothingTouchedAsync();
    }

    [Fact]
    public async Task Takes_a_hundred_ids()
    {
        var request = new StaleCopyDeletionRequestModel { OrganizationIds = Ids(40), UserIds = Ids(60) };

        Assert.Equal(100, Answer(await _sut.DeleteAsync(request)).AlreadyGone);
    }

    [Fact]
    public async Task Refuses_a_request_without_a_body()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => _sut.DeleteAsync(null));
    }

    [Fact]
    public async Task Is_not_there_when_no_client_may_use_it()
    {
        _globalSettings.StaleCopyDeletionClientSecret.Returns((string)null!);

        Assert.IsType<NotFoundResult>(await _sut.DeleteAsync(Request()));
        await AssertNothingTouchedAsync();
    }

    [Fact]
    public async Task Is_not_there_without_an_internal_listener()
    {
        _globalSettings.StaleCopyDeletionPort.Returns((int?)null);

        Assert.IsType<NotFoundResult>(await _sut.DeleteAsync(Request()));
        await AssertNothingTouchedAsync();
    }

    [Theory]
    [InlineData(33656)]
    [InlineData(0)]
    public async Task Is_not_there_on_any_listener_but_the_internal_one(int port)
    {
        _sut.HttpContext.Connection.LocalPort = port;

        Assert.IsType<NotFoundResult>(await _sut.DeleteAsync(Request()));
        await _tokenValidator.DidNotReceiveWithAnyArgs().ValidateAccessTokenAsync(default!, default);
        await AssertNothingTouchedAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Basic YTpi")]
    [InlineData("Bearer ")]
    public async Task Refuses_a_request_without_a_bearer_token(string? authorization)
    {
        _sut.HttpContext.Request.Headers.Authorization = authorization;

        Assert.IsType<UnauthorizedResult>(await _sut.DeleteAsync(Request()));
        await AssertNothingTouchedAsync();
    }

    [Fact]
    public async Task Refuses_a_token_that_does_not_validate_for_the_deletion_scope()
    {
        // Another internal client's token, say: it was issued for internal.notifications alone.
        Bearer("another-client-token");
        _tokenValidator.ValidateAccessTokenAsync("another-client-token", ApiScopes.InternalStaleCopyDeletion)
            .Returns(new TokenValidationResult { IsError = true, Error = "insufficient_scope" });

        Assert.IsType<UnauthorizedResult>(await _sut.DeleteAsync(Request()));
        await AssertNothingTouchedAsync();
    }

    [Theory]
    [InlineData("web")]
    [InlineData("installation.other-service")]
    [InlineData("installation.internal-service")]
    [InlineData("release")]
    [InlineData("releaseinternal-service")]
    public async Task Refuses_a_token_issued_to_any_other_client(string client)
    {
        Validates(Token, client);

        Assert.IsType<UnauthorizedResult>(await _sut.DeleteAsync(Request()));
        await AssertNothingTouchedAsync();
    }

    [Fact]
    public async Task Logs_the_client_and_counts_alone()
    {
        var organization = new Organization { Id = Guid.NewGuid(), Name = "Secret Name", BillingEmail = "billing@example.com" };
        var user = new User { Id = Guid.NewGuid(), Email = "person@example.com", Key = "2.a-wrapped-user-key" };
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        _userRepository.GetByIdAsync(user.Id).Returns(user);
        _cipherRepository.GetManyByUserIdAsync(user.Id, false).Returns(
            [new CipherDetails { Id = Guid.NewGuid(), UserId = user.Id, Data = "2.encrypted-data", Attachments = WithAttachment }]);

        await _sut.DeleteAsync(new StaleCopyDeletionRequestModel { OrganizationIds = [organization.Id], UserIds = [user.Id] });

        var logged = _logger.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ILogger.Log))
            .Select(call => call.GetArguments()[2]!.ToString()!)
            .ToList();
        Assert.Equal(
        [
            $"Deleted at internal client {Client}'s request: 0 providers, 1 organizations and 1 users "
            + "deleted, 0 already gone, attachment files of 1 ciphers deleted.",
        ], logged);
        var everything = string.Join("\n", _logger.ReceivedCalls().SelectMany(call => call.GetArguments())
            .Select(argument => argument?.ToString() ?? string.Empty));
        Assert.DoesNotContain("example", everything);
        Assert.DoesNotContain("Secret", everything);
        Assert.DoesNotContain("encrypted", everything);
        Assert.DoesNotContain("wrapped", everything);
        Assert.DoesNotContain(user.Id.ToString(), everything);
        Assert.DoesNotContain(organization.Id.ToString(), everything);
    }

    [Fact]
    public void Answers_on_its_internal_route_alone()
    {
        var type = typeof(StaleCopyDeletionController);
        Assert.Equal("internal/stale-copies",
            type.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>().Single().Template);
        Assert.True(type.GetCustomAttributes(typeof(ApiExplorerSettingsAttribute), false)
            .Cast<ApiExplorerSettingsAttribute>().Single().IgnoreApi);
        Assert.Single(type.GetCustomAttributes(typeof(Bit.SharedWeb.Utilities.ExceptionHandlerFilterAttribute), false));
        var post = type.GetMethod(nameof(StaleCopyDeletionController.DeleteAsync))!
            .GetCustomAttributes(typeof(HttpPostAttribute), false).Cast<HttpPostAttribute>().Single();
        Assert.Equal("delete", post.Template);
    }

    private static List<Guid> Ids(int count) => Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

    private static StaleCopyDeletionRequestModel Request() =>
        new() { ProviderIds = [Guid.NewGuid()], OrganizationIds = [Guid.NewGuid()], UserIds = [Guid.NewGuid()] };

    private async Task AssertNothingTouchedAsync()
    {
        await _providerRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
        await _providerRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
        await _organizationRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
        await _organizationRepository.DidNotReceiveWithAnyArgs().DeleteAndCreateDeleteTasksAsync(default!, default!);
        await _userRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
        await _userRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
        await _attachmentStorageService.DidNotReceiveWithAnyArgs().DeleteAttachmentsForCipherAsync(default);
        await _sendFileStorageService.DidNotReceiveWithAnyArgs().DeleteFilesForOrganizationAsync(default);
        await _sendFileStorageService.DidNotReceiveWithAnyArgs().DeleteFilesForUserAsync(default);
    }

    private static StaleCopyDeletionResponseModel Answer(IActionResult result) =>
        Assert.IsType<StaleCopyDeletionResponseModel>(Assert.IsType<OkObjectResult>(result).Value);

    private void Bearer(string token) =>
        _sut.HttpContext.Request.Headers.Authorization = $"Bearer {token}";

    private void Validates(string token, string client) =>
        _tokenValidator.ValidateAccessTokenAsync(token, ApiScopes.InternalStaleCopyDeletion)
            .Returns(new TokenValidationResult
            {
                IsError = false,
                Claims = [new Claim(JwtClaimTypes.ClientId, client), new Claim(JwtClaimTypes.Scope, ApiScopes.InternalStaleCopyDeletion)],
            });
}
