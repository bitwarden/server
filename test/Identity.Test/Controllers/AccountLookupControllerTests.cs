using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Identity.Controllers;
using Bit.Identity.IdentityServer.ClientProviders;
using Bit.Identity.Models.Request.Accounts;
using Bit.Identity.Models.Response.Accounts;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Identity.Test.Controllers;

public class AccountLookupControllerTests : IDisposable
{
    private const string Token = "a-token";

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IOrganizationRepository _organizations = Substitute.For<IOrganizationRepository>();
    private readonly IOrganizationDomainRepository _domains = Substitute.For<IOrganizationDomainRepository>();
    private readonly ITokenValidator _tokenValidator = Substitute.For<ITokenValidator>();
    private readonly GlobalSettings _globalSettings = Substitute.For<GlobalSettings>();
    private readonly AccountLookupController _sut;

    public AccountLookupControllerTests()
    {
        _globalSettings.AccountLookupClientSecret.Returns("a-secret");
        _sut = new AccountLookupController(_userRepository, _organizations, _domains, _tokenValidator, _globalSettings)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        Bearer(Token);
        Validates(Token, $"{AccountLookupClientProvider.Prefix}.internal-service");
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task Answers_whether_an_account_has_the_address_and_whether_its_domain_is_blocked()
    {
        var organization = Guid.NewGuid();
        var created = new DateTime(2026, 10, 19, 12, 30, 15, DateTimeKind.Unspecified);
        var account = Guid.NewGuid();
        _userRepository.GetByEmailAsync("person@example.com").Returns(new User { Id = account, Email = "person@example.com", CreationDate = created });
        _domains.HasVerifiedDomainWithBlockClaimedDomainPolicyAsync("example.com", organization).Returns(true);

        var result = await _sut.PostLookupAsync(new AccountLookupRequestModel
        {
            Email = " person@example.com ",
            ExcludeOrganizationId = organization,
        });

        var answer = Assert.IsType<AccountLookupResponseModel>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.True(answer.AccountExists);
        Assert.Equal(account, answer.AccountId);
        Assert.True(answer.AccountCreationBlocked);
        // As stored, in UTC, and marked so.
        Assert.Equal(created, answer.AccountCreationDate);
        Assert.Equal(DateTimeKind.Utc, answer.AccountCreationDate!.Value.Kind);
        Assert.Equal("accountLookup", answer.Object);
    }

    [Fact]
    public async Task Answers_no_for_an_address_nobody_has_on_a_domain_nobody_blocks()
    {
        _userRepository.GetByEmailAsync(Arg.Any<string>()).ReturnsNull();
        _domains.HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(Arg.Any<string>(), Arg.Any<Guid?>()).Returns(false);

        var result = await _sut.PostLookupAsync(new AccountLookupRequestModel { Email = "nobody@Example.COM" });

        var answer = Assert.IsType<AccountLookupResponseModel>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.False(answer.AccountExists);
        Assert.Null(answer.AccountId);
        Assert.False(answer.AccountCreationBlocked);
        Assert.Null(answer.AccountCreationDate);
        // The domain as this server's registration compares it (EmailValidation.GetDomain), nobody excluded.
        await _domains.Received(1).HasVerifiedDomainWithBlockClaimedDomainPolicyAsync("example.com", null);
    }

    [Fact]
    public async Task Is_not_there_when_no_client_may_use_it()
    {
        _globalSettings.AccountLookupClientSecret.Returns((string)null!);

        var result = await _sut.PostLookupAsync(new AccountLookupRequestModel { Email = "person@example.com" });

        Assert.IsType<NotFoundResult>(result);
        await _userRepository.DidNotReceiveWithAnyArgs().GetByEmailAsync(default!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Basic YTpi")]
    [InlineData("Bearer ")]
    public async Task Refuses_a_request_without_a_bearer_token(string? authorization)
    {
        _sut.HttpContext.Request.Headers.Authorization = authorization;

        var result = await _sut.PostLookupAsync(new AccountLookupRequestModel { Email = "person@example.com" });

        Assert.IsType<UnauthorizedResult>(result);
        await _userRepository.DidNotReceiveWithAnyArgs().GetByEmailAsync(default!);
        await _domains.DidNotReceiveWithAnyArgs().HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(default!, default);
    }

    [Fact]
    public async Task Refuses_a_token_that_does_not_validate_for_the_lookup_scope()
    {
        Bearer("another-token");
        _tokenValidator.ValidateAccessTokenAsync("another-token", ApiScopes.InternalAccountLookup)
            .Returns(new TokenValidationResult { IsError = true, Error = "insufficient_scope" });

        var result = await _sut.PostLookupAsync(new AccountLookupRequestModel { Email = "person@example.com" });

        Assert.IsType<UnauthorizedResult>(result);
        await _userRepository.DidNotReceiveWithAnyArgs().GetByEmailAsync(default!);
    }

    [Theory]
    [InlineData("web")]
    [InlineData("internal.notifications")]
    [InlineData("lookupinternal-service")]
    public async Task Refuses_a_token_issued_to_any_other_client(string client)
    {
        Validates(Token, client);

        var result = await _sut.PostLookupAsync(new AccountLookupRequestModel { Email = "person@example.com" });

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Refuses_an_address_without_a_domain_in_this_server_s_words()
    {
        var failure = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.PostLookupAsync(new AccountLookupRequestModel { Email = "not an address" }));

        Assert.Equal("Invalid email address format.", failure.Message);
    }

    [Fact]
    public async Task Names_the_account_its_collation_finds_for_an_address_without_a_domain()
    {
        var account = Guid.NewGuid();
        _userRepository.GetByEmailAsync("victim\uFF20example.com").Returns(new User { Id = account, Email = "victim@example.com" });

        var result = await _sut.PostLookupAsync(new AccountLookupRequestModel { Email = "victim\uFF20example.com" });

        var answer = Assert.IsType<AccountLookupResponseModel>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.True(answer.AccountExists);
        Assert.Equal(account, answer.AccountId);
        Assert.False(answer.AccountCreationBlocked);
        await _domains.DidNotReceiveWithAnyArgs().HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(default!, default);
    }

    [Fact]
    public async Task Answers_which_organization_uses_an_identifier()
    {
        var organization = Guid.NewGuid();
        _organizations.GetByIdentifierAsync("acme").Returns(new Organization { Id = organization, Identifier = "acme" });

        var result = await _sut.PostOrganizationIdentifierLookupAsync(new OrganizationIdentifierLookupRequestModel { Identifier = " acme " });

        var answer = Assert.IsType<OrganizationIdentifierLookupResponseModel>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(organization, answer.OrganizationId);
        Assert.Equal("organizationIdentifierLookup", answer.Object);
    }

    [Fact]
    public async Task Answers_nobody_for_an_identifier_no_organization_uses()
    {
        _organizations.GetByIdentifierAsync(Arg.Any<string>()).ReturnsNull();

        var result = await _sut.PostOrganizationIdentifierLookupAsync(new OrganizationIdentifierLookupRequestModel { Identifier = "free" });

        var answer = Assert.IsType<OrganizationIdentifierLookupResponseModel>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Null(answer.OrganizationId);
    }

    [Fact]
    public async Task Identifier_lookup_is_not_there_when_no_client_may_use_it()
    {
        _globalSettings.AccountLookupClientSecret.Returns((string)null!);

        var result = await _sut.PostOrganizationIdentifierLookupAsync(new OrganizationIdentifierLookupRequestModel { Identifier = "acme" });

        Assert.IsType<NotFoundResult>(result);
        await _organizations.DidNotReceiveWithAnyArgs().GetByIdentifierAsync(default!);
    }

    [Theory]
    [InlineData("web")]
    [InlineData("internal.notifications")]
    public async Task Identifier_lookup_refuses_a_token_issued_to_any_other_client(string client)
    {
        Validates(Token, client);

        var result = await _sut.PostOrganizationIdentifierLookupAsync(new OrganizationIdentifierLookupRequestModel { Identifier = "acme" });

        Assert.IsType<UnauthorizedResult>(result);
        await _organizations.DidNotReceiveWithAnyArgs().GetByIdentifierAsync(default!);
    }

    [Fact]
    public void Names_nothing_when_logged()
    {
        Assert.Equal(nameof(AccountLookupRequestModel), new AccountLookupRequestModel { Email = "person@example.com" }.ToString());
    }

    private void Bearer(string token) =>
        _sut.HttpContext.Request.Headers.Authorization = $"Bearer {token}";

    private void Validates(string token, string client) =>
        _tokenValidator.ValidateAccessTokenAsync(token, ApiScopes.InternalAccountLookup)
            .Returns(new TokenValidationResult
            {
                IsError = false,
                Claims = [new Claim(JwtClaimTypes.ClientId, client), new Claim(JwtClaimTypes.Scope, ApiScopes.InternalAccountLookup)],
            });
}
