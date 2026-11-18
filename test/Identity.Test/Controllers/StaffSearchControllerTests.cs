using System.Security.Claims;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.HttpExtensions;
using Bit.Identity.Controllers;
using Bit.Identity.Models.Response.StaffSearch;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.Controllers;

public class StaffSearchControllerTests : IDisposable
{
    private const string Token = "a-token";
    private const string Client = "search.other-service";
    private const int InternalPort = 33657;

    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IProviderRepository _providerRepository = Substitute.For<IProviderRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ITwoFactorIsEnabledQuery _twoFactorIsEnabledQuery = Substitute.For<ITwoFactorIsEnabledQuery>();
    private readonly ITokenValidator _tokenValidator = Substitute.For<ITokenValidator>();
    private readonly GlobalSettings _globalSettings = Substitute.For<GlobalSettings>();
    private readonly ILogger<StaffSearchController> _logger = Substitute.For<ILogger<StaffSearchController>>();
    private readonly StaffSearchController _sut;

    public StaffSearchControllerTests()
    {
        _globalSettings.StaffSearchClientSecret.Returns("a-secret");
        _globalSettings.StaffSearchPort.Returns(InternalPort);
        _organizationRepository.SearchAsync(default!, default!, default, default, default).ReturnsForAnyArgs([]);
        _organizationRepository.SearchUnassignedToProviderAsync(default!, default!, default, default).ReturnsForAnyArgs([]);
        _providerRepository.SearchAsync(default!, default!, default, default).ReturnsForAnyArgs([]);
        _userRepository.SearchAsync(default!, default, default).ReturnsForAnyArgs([]);
        _twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<Guid>>()).Returns([]);
        _sut = new StaffSearchController(_organizationRepository, _providerRepository, _userRepository,
            _twoFactorIsEnabledQuery, _tokenValidator, _globalSettings, _logger)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        _sut.HttpContext.Connection.LocalPort = InternalPort;
        Bearer(Token);
        Validates(Token, Client);
    }

    public void Dispose() => _sut.Dispose();

    public static TheoryData<string> Endpoints => new("organizations", "unassigned-organizations", "providers", "users");

    [Fact]
    public async Task Searches_organizations_as_the_index_does_with_the_name_html_encoded()
    {
        await _sut.SearchOrganizationsAsync("Smith & <Sons>", "owner@example.com", true, 50, 25);

        await _organizationRepository.Received(1)
            .SearchAsync("Smith &amp; &lt;Sons&gt;", "owner@example.com", true, 50, 25);
    }

    [Fact]
    public async Task Searches_organizations_with_empty_values_as_no_filter()
    {
        await _sut.SearchOrganizationsAsync("", "", null);

        await _organizationRepository.Received(1).SearchAsync(null!, null!, null, 0, 25);
    }

    [Fact]
    public async Task Answers_of_each_organization_what_the_index_shows()
    {
        var paid = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Smith &amp; Sons",
            Plan = "Enterprise (Annually)",
            Seats = 40,
            CreationDate = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            GatewayCustomerId = "cus_a",
            GatewaySubscriptionId = "sub_a",
            Enabled = true,
            ExpirationDate = new DateTime(2027, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Storage = 1610612736, // 1.5 GB
            Use2fa = true,
        };
        paid.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.OrganizationDuo] = new() { Enabled = true },
        });
        var free = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Free",
            Plan = "Free",
            CreationDate = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Enabled = false,
        };
        _organizationRepository.SearchAsync(default!, default!, default, default, default).ReturnsForAnyArgs([paid, free]);

        var result = await _sut.SearchOrganizationsAsync();

        var list = Answer<StaffSearchOrganizationResponseModel>(result);
        Assert.Equal("list", list.Object);
        var items = list.Data.ToList();
        Assert.Equal(2, items.Count);
        var first = items[0];
        Assert.Equal("staffSearchOrganization", first.Object);
        Assert.Equal(paid.Id, first.Id);
        Assert.Equal("Smith &amp; Sons", first.Name);
        Assert.Equal("Enterprise (Annually)", first.Plan);
        Assert.Equal(40, first.Seats);
        Assert.Equal(paid.CreationDate, first.CreationDate);
        Assert.True(first.Paid);
        Assert.True(first.Enabled);
        Assert.Equal(paid.ExpirationDate, first.ExpirationDate);
        Assert.True(first.TwoFactorEnabled);
        Assert.Equal(1.5, first.StorageGb);
        var second = items[1];
        Assert.False(second.Paid);
        Assert.False(second.Enabled);
        Assert.Null(second.ExpirationDate);
        Assert.Null(second.Seats);
        Assert.False(second.TwoFactorEnabled);
        Assert.Equal(0, second.StorageGb);
    }

    [Fact]
    public async Task Rounds_an_organization_s_storage_to_two_places_as_the_index_does()
    {
        _organizationRepository.SearchAsync(default!, default!, default, default, default).ReturnsForAnyArgs(
            [new Organization { Name = "A", Plan = "Free", Storage = 1234567890 }]);

        var item = Answer<StaffSearchOrganizationResponseModel>(await _sut.SearchOrganizationsAsync()).Data.Single();

        Assert.Equal(1.15, item.StorageGb);
    }

    [Fact]
    public async Task Never_answers_an_organization_s_gateway_identifiers_or_keys()
    {
        _organizationRepository.SearchAsync(default!, default!, default, default, default).ReturnsForAnyArgs(
        [
            new Organization
            {
                Name = "A",
                Plan = "Teams",
                GatewayCustomerId = "cus_secret",
                GatewaySubscriptionId = "sub_secret",
                PublicKey = "a-public-key",
                PrivateKey = "a-private-key",
                LicenseKey = "a-licence-key",
                BillingEmail = "billing@example.com",
            },
        ]);

        var json = Json(await _sut.SearchOrganizationsAsync());

        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("-key", json);
        Assert.DoesNotContain("billing@", json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["object", "data", "continuationToken"], document.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(
            ["object", "id", "name", "plan", "seats", "creationDate", "paid", "enabled", "expirationDate", "twoFactorEnabled", "storageGb"],
            document.RootElement.GetProperty("data")[0].EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task Searches_unassigned_organizations_as_the_list_does_with_the_name_html_encoded()
    {
        await _sut.SearchUnassignedOrganizationsAsync("Smith & <Sons>", "owner@", 50, 25);

        await _organizationRepository.Received(1)
            .SearchUnassignedToProviderAsync("Smith &amp; &lt;Sons&gt;", "owner@", 50, 25);
    }

    [Fact]
    public async Task Searches_unassigned_organizations_with_empty_values_as_no_filter()
    {
        await _sut.SearchUnassignedOrganizationsAsync("", "");

        await _organizationRepository.Received(1).SearchUnassignedToProviderAsync(null!, null!, 0, 25);
    }

    [Fact]
    public async Task Answers_of_each_unassigned_organization_what_the_list_shows_and_nothing_else()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Smith &amp;amp; Sons",
            Plan = "Teams (Monthly)",
            PlanType = PlanType.TeamsMonthly,
            Seats = 10,
            GatewayCustomerId = "cus_secret",
            GatewaySubscriptionId = "sub_secret",
            PublicKey = "a-public-key",
            PrivateKey = "a-private-key",
            LicenseKey = "a-licence-key",
            BillingEmail = "billing@example.com",
        };
        _organizationRepository.SearchUnassignedToProviderAsync(default!, default!, default, default)
            .ReturnsForAnyArgs([organization]);

        var result = await _sut.SearchUnassignedOrganizationsAsync();

        var item = Answer<StaffSearchUnassignedOrganizationResponseModel>(result).Data.Single();
        Assert.Equal("staffSearchUnassignedOrganization", item.Object);
        Assert.Equal(organization.Id, item.Id);
        // Decoded once, as the list's model holds it; its view decodes it again.
        Assert.Equal("Smith &amp; Sons", item.Name);
        Assert.Equal(PlanType.TeamsMonthly, item.PlanType);
        var json = Json(result);
        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("-key", json);
        Assert.DoesNotContain("billing@", json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            ["object", "id", "name", "planType"],
            document.RootElement.GetProperty("data")[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal((int)PlanType.TeamsMonthly, document.RootElement.GetProperty("data")[0].GetProperty("planType").GetInt32());
    }

    [Fact]
    public async Task Searches_providers_as_the_index_does()
    {
        // The providers index passes the name as it came.
        await _sut.SearchProvidersAsync("Smith & Sons", "admin@example.com", 10, 5);

        await _providerRepository.Received(1).SearchAsync("Smith & Sons", "admin@example.com", 10, 5);
    }

    [Fact]
    public async Task Searches_providers_with_empty_values_as_no_filter()
    {
        await _sut.SearchProvidersAsync("", "");

        await _providerRepository.Received(1).SearchAsync(null!, null!, 0, 25);
    }

    [Fact]
    public async Task Answers_of_each_provider_what_the_index_shows()
    {
        var provider = new Provider
        {
            Id = Guid.NewGuid(),
            Name = "Smith &amp; Sons",
            Type = ProviderType.Msp,
            Status = ProviderStatusType.Billable,
            BillingEmail = "billing@example.com",
            GatewayCustomerId = "cus_secret",
            GatewaySubscriptionId = "sub_secret",
        };
        var pending = new Provider { Id = Guid.NewGuid(), Status = ProviderStatusType.Pending };
        _providerRepository.SearchAsync(default!, default!, default, default).ReturnsForAnyArgs([provider, pending]);

        var result = await _sut.SearchProvidersAsync();

        var items = Answer<StaffSearchProviderResponseModel>(result).Data.ToList();
        Assert.Equal("staffSearchProvider", items[0].Object);
        Assert.Equal(provider.Id, items[0].Id);
        Assert.Equal("Smith &amp; Sons", items[0].Name);
        Assert.Equal(ProviderType.Msp, items[0].Type);
        Assert.Equal(ProviderStatusType.Billable, items[0].Status);
        Assert.Equal(provider.CreationDate, items[0].CreationDate);
        Assert.Null(items[1].Name);
        var json = Json(result);
        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("billing@", json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            ["object", "id", "name", "type", "status", "creationDate"],
            document.RootElement.GetProperty("data")[0].EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task Searches_users_by_address_as_the_index_does()
    {
        await _sut.SearchUsersAsync("person@", 20, 10);

        await _userRepository.Received(1).SearchAsync("person@", 20, 10);
    }

    // Unescaped, LIKE '%%' would list every account.
    [Fact]
    public async Task Searches_users_by_the_address_taken_literally()
    {
        _globalSettings.DatabaseProvider.Returns("sqlServer");

        await _sut.SearchUsersAsync("%", 0, 25);

        await _userRepository.Received(1).SearchAsync("[%]", 0, 25);
    }

    [Theory]
    [InlineData(null, "a_b%c[d@", "a[_]b[%]c[[]d@")]
    [InlineData("sqlServer", "john_doe@", "john[_]doe@")]
    [InlineData("postgres", @"a_b%c\d@", @"a\_b\%c\\d@")]
    [InlineData("PostgreSQL", "%", @"\%")]
    [InlineData("mysql", "a_b%@", "a_b%@")]
    [InlineData("mariadb", "a_b%@", "a_b%@")]
    [InlineData("sqlite", "a_b%@", "a_b%@")]
    [InlineData("sqlServer", "person@example.com", "person@example.com")]
    public void Escapes_the_address_as_each_provider_s_search_matches_it(string? provider, string email, string expected)
    {
        Assert.Equal(expected, StaffSearchController.LiteralPrefix(email, provider));
    }

    [Fact]
    public async Task Answers_of_each_user_what_the_index_shows_with_two_step_login_as_its_lookup_answers()
    {
        var premium = new User
        {
            Id = Guid.NewGuid(),
            Email = "person@example.com",
            Name = "A Person",
            CreationDate = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Premium = true,
            PremiumExpirationDate = new DateTime(2027, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            MaxStorageGb = 5,
            EmailVerified = true,
            Key = "a-protected-user-key",
            PrivateKey = "a-protected-private-key",
            MasterPassword = "a-hash",
            SecurityStamp = "a-stamp",
            GatewayCustomerId = "cus_secret",
            GatewaySubscriptionId = "sub_secret",
            LicenseKey = "a-licence-key",
        };
        var plain = new User { Id = Guid.NewGuid(), Email = "person2@example.com" };
        _userRepository.SearchAsync(default!, default, default).ReturnsForAnyArgs([premium, plain]);
        _twoFactorIsEnabledQuery
            .TwoFactorIsEnabledAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { premium.Id, plain.Id })))
            .Returns([(premium.Id, true), (plain.Id, false)]);

        var result = await _sut.SearchUsersAsync("person");

        var items = Answer<StaffSearchUserResponseModel>(result).Data.ToList();
        Assert.Equal("staffSearchUser", items[0].Object);
        Assert.Equal(premium.Id, items[0].Id);
        Assert.Equal("person@example.com", items[0].Email);
        Assert.Equal(premium.CreationDate, items[0].CreationDate);
        Assert.True(items[0].Premium);
        Assert.Equal(premium.PremiumExpirationDate, items[0].PremiumExpirationDate);
        Assert.Equal((short)5, items[0].MaxStorageGb);
        Assert.True(items[0].EmailVerified);
        Assert.True(items[0].TwoFactorEnabled);
        Assert.False(items[1].Premium);
        Assert.Null(items[1].PremiumExpirationDate);
        Assert.Null(items[1].MaxStorageGb);
        Assert.False(items[1].EmailVerified);
        Assert.False(items[1].TwoFactorEnabled);
        var json = Json(result);
        Assert.DoesNotContain("protected", json);
        Assert.DoesNotContain("a-hash", json);
        Assert.DoesNotContain("a-stamp", json);
        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("licence", json);
        Assert.DoesNotContain("A Person", json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            ["object", "id", "email", "creationDate", "premium", "premiumExpirationDate", "maxStorageGb", "emailVerified", "twoFactorEnabled"],
            document.RootElement.GetProperty("data")[0].EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task Answers_two_step_login_off_for_a_user_its_lookup_does_not_name()
    {
        _userRepository.SearchAsync(default!, default, default).ReturnsForAnyArgs([new User { Id = Guid.NewGuid(), Email = "a@example.com" }]);

        var item = Answer<StaffSearchUserResponseModel>(await _sut.SearchUsersAsync("a")).Data.Single();

        Assert.False(item.TwoFactorEnabled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Refuses_a_user_search_without_an_address(string? email)
    {
        var refused = await Assert.ThrowsAsync<BadRequestException>(() => _sut.SearchUsersAsync(email));

        Assert.Equal("email is required.", refused.Message);
        await _userRepository.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, default);
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Refuses_a_page_outside_the_bounds(string endpoint, int skip, int take)
    {
        await Assert.ThrowsAsync<BadRequestException>(() => Search(endpoint, skip, take));

        await NothingSearched();
    }

    public static TheoryData<string, int, int> Pages()
    {
        var pages = new TheoryData<string, int, int>();
        foreach (var endpoint in new[] { "organizations", "unassigned-organizations", "providers", "users" })
        {
            pages.Add(endpoint, -1, 25);
            pages.Add(endpoint, 0, 0);
            pages.Add(endpoint, 0, -1);
            pages.Add(endpoint, 0, 101);
        }

        return pages;
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 100)]
    [InlineData(1000, 100)]
    public async Task Answers_a_page_within_the_bounds(int skip, int take)
    {
        await _sut.SearchOrganizationsAsync(skip: skip, take: take);
        await _sut.SearchUnassignedOrganizationsAsync(skip: skip, take: take);
        await _sut.SearchProvidersAsync(skip: skip, take: take);
        await _sut.SearchUsersAsync("a", skip, take);

        await _organizationRepository.Received(1).SearchAsync(null!, null!, null, skip, take);
        await _organizationRepository.Received(1).SearchUnassignedToProviderAsync(null!, null!, skip, take);
        await _providerRepository.Received(1).SearchAsync(null!, null!, skip, take);
        await _userRepository.Received(1).SearchAsync("a", skip, take);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Is_not_there_when_no_client_may_use_it(string endpoint)
    {
        _globalSettings.StaffSearchClientSecret.Returns((string)null!);

        Assert.IsType<NotFoundResult>(await Search(endpoint));

        await _tokenValidator.DidNotReceiveWithAnyArgs().ValidateAccessTokenAsync(default!, default);
        await NothingSearched();
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Is_not_there_without_an_internal_listener(string endpoint)
    {
        _globalSettings.StaffSearchPort.Returns((int?)null);

        Assert.IsType<NotFoundResult>(await Search(endpoint));

        await NothingSearched();
    }

    [Theory]
    [InlineData("organizations", 33656)]
    [InlineData("unassigned-organizations", 33656)]
    [InlineData("providers", 33656)]
    [InlineData("users", 33656)]
    [InlineData("users", 0)]
    public async Task Is_not_there_on_any_listener_but_the_internal_one(string endpoint, int port)
    {
        _sut.HttpContext.Connection.LocalPort = port;

        Assert.IsType<NotFoundResult>(await Search(endpoint));

        await _tokenValidator.DidNotReceiveWithAnyArgs().ValidateAccessTokenAsync(default!, default);
        await NothingSearched();
    }

    [Fact]
    public async Task Is_not_there_before_a_bad_page_is_looked_at()
    {
        _sut.HttpContext.Connection.LocalPort = 33656;

        Assert.IsType<NotFoundResult>(await _sut.SearchUsersAsync(null, -1, 0));
    }

    [Theory]
    [InlineData("organizations", null)]
    [InlineData("organizations", "")]
    [InlineData("unassigned-organizations", null)]
    [InlineData("providers", "Basic YTpi")]
    [InlineData("users", "Bearer ")]
    [InlineData("users", null)]
    public async Task Refuses_a_request_without_a_bearer_token(string endpoint, string? authorization)
    {
        _sut.HttpContext.Request.Headers.Authorization = authorization;

        Assert.IsType<UnauthorizedResult>(await Search(endpoint));

        await NothingSearched();
    }

    [Fact]
    public async Task Refuses_an_unauthorized_request_before_a_bad_page_is_looked_at()
    {
        _sut.HttpContext.Request.Headers.Authorization = default;

        Assert.IsType<UnauthorizedResult>(await _sut.SearchUsersAsync(null, -1, 0));
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Refuses_a_token_that_does_not_validate_for_the_search_scope(string endpoint)
    {
        // Another internal client's token, say: it was issued for internal.notifications alone.
        Bearer("another-client-token");
        _tokenValidator.ValidateAccessTokenAsync("another-client-token", ApiScopes.InternalStaffSearch)
            .Returns(new TokenValidationResult { IsError = true, Error = "insufficient_scope" });

        Assert.IsType<UnauthorizedResult>(await Search(endpoint));

        await NothingSearched();
    }

    [Theory]
    [InlineData("web")]
    [InlineData("internal.notifications")]
    [InlineData("installation.other-service")]
    [InlineData("organization.other-service")]
    [InlineData("searchother-service")]
    [InlineData("Search.other-service")]
    public async Task Refuses_a_token_issued_to_any_other_client(string client)
    {
        Validates(Token, client);

        Assert.IsType<UnauthorizedResult>(await _sut.SearchOrganizationsAsync());
        Assert.IsType<UnauthorizedResult>(await _sut.SearchUnassignedOrganizationsAsync());
        Assert.IsType<UnauthorizedResult>(await _sut.SearchProvidersAsync());
        Assert.IsType<UnauthorizedResult>(await _sut.SearchUsersAsync("a"));

        await NothingSearched();
    }

    [Fact]
    public async Task Refuses_a_token_naming_no_client()
    {
        _tokenValidator.ValidateAccessTokenAsync(Token, ApiScopes.InternalStaffSearch)
            .Returns(new TokenValidationResult { IsError = false, Claims = [] });

        Assert.IsType<UnauthorizedResult>(await _sut.SearchOrganizationsAsync());
    }

    [Fact]
    public async Task Logs_the_client_and_the_count_and_never_a_search_term()
    {
        _organizationRepository.SearchAsync(default!, default!, default, default, default).ReturnsForAnyArgs(
            [new Organization { Name = "Org Name", Plan = "Free" }]);
        _organizationRepository.SearchUnassignedToProviderAsync(default!, default!, default, default).ReturnsForAnyArgs(
            [new Organization { Name = "Unassigned Name" }]);
        _providerRepository.SearchAsync(default!, default!, default, default).ReturnsForAnyArgs(
            [new Provider { Name = "Provider Name" }, new Provider { Name = "Another" }]);
        _userRepository.SearchAsync(default!, default, default).ReturnsForAnyArgs(
            [new User { Email = "found@example.com" }]);

        await _sut.SearchOrganizationsAsync("org-term", "org-member@example.com", true);
        await _sut.SearchUnassignedOrganizationsAsync("unassigned-term", "owner@example.com");
        await _sut.SearchProvidersAsync("provider-term", "provider-member@example.com");
        await _sut.SearchUsersAsync("user-term@");

        var logged = _logger.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ILogger.Log))
            .Select(call => call.GetArguments()[2]!.ToString()!)
            .ToList();
        Assert.Equal(
        [
            $"Internal client {Client}'s staff search of organizations found 1.",
            $"Internal client {Client}'s staff search of unassigned organizations found 1.",
            $"Internal client {Client}'s staff search of providers found 2.",
            $"Internal client {Client}'s staff search of users found 1.",
        ], logged);
    }

    [Fact]
    public void Answers_on_its_internal_routes_alone()
    {
        var route = typeof(StaffSearchController).GetCustomAttributes(typeof(RouteAttribute), false)
            .Cast<RouteAttribute>().Single();
        Assert.Equal("internal/staff-search", route.Template);
        Assert.Equal("organizations", GetTemplate(nameof(StaffSearchController.SearchOrganizationsAsync)));
        Assert.Equal("unassigned-organizations", GetTemplate(nameof(StaffSearchController.SearchUnassignedOrganizationsAsync)));
        Assert.Equal("providers", GetTemplate(nameof(StaffSearchController.SearchProvidersAsync)));
        Assert.Equal("users", GetTemplate(nameof(StaffSearchController.SearchUsersAsync)));
    }

    private static string? GetTemplate(string action) =>
        typeof(StaffSearchController).GetMethod(action)!
            .GetCustomAttributes(typeof(HttpGetAttribute), false).Cast<HttpGetAttribute>().Single().Template;

    private Task<IActionResult> Search(string endpoint, int skip = 0, int take = 25) => endpoint switch
    {
        "organizations" => _sut.SearchOrganizationsAsync(skip: skip, take: take),
        "unassigned-organizations" => _sut.SearchUnassignedOrganizationsAsync(skip: skip, take: take),
        "providers" => _sut.SearchProvidersAsync(skip: skip, take: take),
        "users" => _sut.SearchUsersAsync("person@", skip, take),
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint)),
    };

    private async Task NothingSearched()
    {
        await _organizationRepository.DidNotReceiveWithAnyArgs().SearchAsync(default!, default!, default, default, default);
        await _organizationRepository.DidNotReceiveWithAnyArgs().SearchUnassignedToProviderAsync(default!, default!, default, default);
        await _providerRepository.DidNotReceiveWithAnyArgs().SearchAsync(default!, default!, default, default);
        await _userRepository.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, default);
    }

    private static ListResponseModel<T> Answer<T>(IActionResult result) where T : ResponseModel =>
        Assert.IsType<ListResponseModel<T>>(Assert.IsType<OkObjectResult>(result).Value);

    // As MVC writes it: System.Text.Json's web defaults.
    private static string Json(IActionResult result) =>
        JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value,
            Assert.IsType<OkObjectResult>(result).Value!.GetType(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private void Bearer(string token) =>
        _sut.HttpContext.Request.Headers.Authorization = $"Bearer {token}";

    private void Validates(string token, string client) =>
        _tokenValidator.ValidateAccessTokenAsync(token, ApiScopes.InternalStaffSearch)
            .Returns(new TokenValidationResult
            {
                IsError = false,
                Claims = [new Claim(JwtClaimTypes.ClientId, client), new Claim(JwtClaimTypes.Scope, ApiScopes.InternalStaffSearch)],
            });
}
