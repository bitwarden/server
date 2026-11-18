using System.Security.Claims;
using System.Text.Json;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
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
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace Bit.Identity.Test.Controllers;

public class JobTriggerRetirementControllerTests : IDisposable
{
    private const string Token = "a-token";
    private const int InternalPort = 33657;
    private const string Client = $"{JobTriggerRetirementClientProvider.Prefix}.migration-service";

    private readonly IEmergencyAccessRepository _emergencyAccessRepository = Substitute.For<IEmergencyAccessRepository>();
    private readonly IOrganizationDomainRepository _organizationDomainRepository = Substitute.For<IOrganizationDomainRepository>();
    private readonly IOrganizationPlanMigrationCohortAssignmentRepository _cohortAssignmentRepository =
        Substitute.For<IOrganizationPlanMigrationCohortAssignmentRepository>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly ITokenValidator _tokenValidator = Substitute.For<ITokenValidator>();
    private readonly GlobalSettings _globalSettings = Substitute.For<GlobalSettings>();
    private readonly ILogger<JobTriggerRetirementController> _logger = Substitute.For<ILogger<JobTriggerRetirementController>>();
    private readonly JobTriggerRetirementController _sut;

    public JobTriggerRetirementControllerTests()
    {
        _globalSettings.JobTriggerRetirementClientSecret.Returns("a-secret");
        _globalSettings.JobTriggerRetirementPort.Returns(InternalPort);
        _emergencyAccessRepository.GetManyDetailsByUserIdsAsync(default!).ReturnsForAnyArgs([]);
        _organizationDomainRepository.GetDomainsByOrganizationIdAsync(default).ReturnsForAnyArgs([]);
        _cohortAssignmentRepository.GetByOrganizationIdAsync(default).ReturnsNullForAnyArgs();
        _sut = new JobTriggerRetirementController(_emergencyAccessRepository, _organizationDomainRepository,
            _cohortAssignmentRepository, _organizationRepository, _tokenValidator, _globalSettings, _logger)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        _sut.HttpContext.Connection.LocalPort = InternalPort;
        Bearer(Token);
        Validates(Token, Client);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task Deletes_the_rows_that_start_this_server_s_jobs_and_answers_the_counts()
    {
        var grantor = Guid.NewGuid();
        var grantee = Guid.NewGuid();
        var organization = Guid.NewGuid();
        var another = Guid.NewGuid();
        // Granted by one user, to the other, and to somebody else: each grant once, whichever side named it.
        var grants = new List<EmergencyAccessDetails>
        {
            new() { Id = Guid.NewGuid(), GrantorId = grantor, GranteeId = grantee, KeyEncrypted = "a-wrapped-key" },
            new() { Id = Guid.NewGuid(), GrantorId = grantor, GranteeId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), GrantorId = Guid.NewGuid(), GranteeId = grantee },
        };
        _emergencyAccessRepository.GetManyDetailsByUserIdsAsync(
                Arg.Is<ICollection<Guid>>(ids => ids.Count == 2 && ids.Contains(grantor) && ids.Contains(grantee)))
            .Returns(grants);
        var domains = new List<OrganizationDomain>
        {
            new() { Id = Guid.NewGuid(), OrganizationId = organization, DomainName = "example.com" },
            new() { Id = Guid.NewGuid(), OrganizationId = organization, DomainName = "example.org" },
        };
        _organizationDomainRepository.GetDomainsByOrganizationIdAsync(organization).Returns(domains);
        var assignment = new OrganizationPlanMigrationCohortAssignment { Id = Guid.NewGuid(), OrganizationId = organization };
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organization).Returns(assignment);

        var result = await _sut.RetireAsync(new JobTriggerRetirementRequestModel
        {
            UserIds = [grantor, grantee, grantor],
            OrganizationIds = [organization, another, organization],
        });

        var answer = Answer(result);
        Assert.Equal(3, answer.EmergencyAccessDeleted);
        Assert.Equal(2, answer.DomainClaimsDeleted);
        Assert.Equal(1, answer.CohortAssignmentsDeleted);
        Assert.Equal(2, answer.OrganizationsSeatSyncCleared);
        Assert.Equal("jobTriggerRetirement", answer.Object);
        await _emergencyAccessRepository.Received(1).DeleteManyAsync(
            Arg.Is<ICollection<Guid>>(ids => ids.Count == 3 && grants.All(grant => ids.Contains(grant.Id))));
        await _organizationDomainRepository.Received(1).DeleteAsync(domains[0]);
        await _organizationDomainRepository.Received(1).DeleteAsync(domains[1]);
        await _organizationDomainRepository.Received(2).DeleteAsync(Arg.Any<OrganizationDomain>());
        await _cohortAssignmentRepository.Received(1).DeleteAsync(assignment);
        await _cohortAssignmentRepository.Received(1).DeleteAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>());
        await _organizationRepository.Received(1).UpdateSuccessfulOrganizationSyncStatusAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == 2 && ids.Contains(organization) && ids.Contains(another)),
            Arg.Any<DateTime>());
    }

    [Fact]
    public async Task Answers_its_counts_in_camel_case()
    {
        var result = await _sut.RetireAsync(new JobTriggerRetirementRequestModel { OrganizationIds = [Guid.NewGuid()] });

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value,
            typeof(JobTriggerRetirementResponseModel), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(0, json.RootElement.GetProperty("emergencyAccessDeleted").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("domainClaimsDeleted").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("cohortAssignmentsDeleted").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("organizationsSeatSyncCleared").GetInt32());
    }

    [Fact]
    public async Task Is_safe_to_repeat_finding_nothing_left_the_second_time()
    {
        var user = Guid.NewGuid();
        var organization = Guid.NewGuid();
        var grant = new EmergencyAccessDetails { Id = Guid.NewGuid(), GrantorId = user };
        var domain = new OrganizationDomain { Id = Guid.NewGuid(), OrganizationId = organization };
        var assignment = new OrganizationPlanMigrationCohortAssignment { Id = Guid.NewGuid(), OrganizationId = organization };
        _emergencyAccessRepository.GetManyDetailsByUserIdsAsync(default!).ReturnsForAnyArgs([grant], new List<EmergencyAccessDetails>());
        _organizationDomainRepository.GetDomainsByOrganizationIdAsync(organization).Returns([domain], new List<OrganizationDomain>());
        _cohortAssignmentRepository.GetByOrganizationIdAsync(organization).Returns(assignment, (OrganizationPlanMigrationCohortAssignment?)null);
        var request = new JobTriggerRetirementRequestModel { UserIds = [user], OrganizationIds = [organization] };

        await _sut.RetireAsync(request);
        _emergencyAccessRepository.ClearReceivedCalls();
        _organizationDomainRepository.ClearReceivedCalls();
        _cohortAssignmentRepository.ClearReceivedCalls();
        var again = Answer(await _sut.RetireAsync(request));

        Assert.Equal(0, again.EmergencyAccessDeleted);
        Assert.Equal(0, again.DomainClaimsDeleted);
        Assert.Equal(0, again.CohortAssignmentsDeleted);
        Assert.Equal(1, again.OrganizationsSeatSyncCleared);
        await _emergencyAccessRepository.DidNotReceiveWithAnyArgs().DeleteManyAsync(default!);
        await _organizationDomainRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
        await _cohortAssignmentRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Does_nothing_for_empty_or_absent_lists(bool absent)
    {
        var request = absent
            ? new JobTriggerRetirementRequestModel()
            : new JobTriggerRetirementRequestModel { UserIds = [], OrganizationIds = [] };

        var answer = Answer(await _sut.RetireAsync(request));

        Assert.Equal(0, answer.EmergencyAccessDeleted);
        Assert.Equal(0, answer.DomainClaimsDeleted);
        Assert.Equal(0, answer.CohortAssignmentsDeleted);
        Assert.Equal(0, answer.OrganizationsSeatSyncCleared);
        await _emergencyAccessRepository.DidNotReceiveWithAnyArgs().GetManyDetailsByUserIdsAsync(default!);
        await _emergencyAccessRepository.DidNotReceiveWithAnyArgs().DeleteManyAsync(default!);
        await _organizationRepository.DidNotReceiveWithAnyArgs().UpdateSuccessfulOrganizationSyncStatusAsync(default!, default);
    }

    [Fact]
    public async Task Users_alone_leave_organizations_alone()
    {
        await _sut.RetireAsync(new JobTriggerRetirementRequestModel { UserIds = [Guid.NewGuid()] });

        await _emergencyAccessRepository.ReceivedWithAnyArgs(1).GetManyDetailsByUserIdsAsync(default!);
        await _organizationDomainRepository.DidNotReceiveWithAnyArgs().GetDomainsByOrganizationIdAsync(default);
        await _cohortAssignmentRepository.DidNotReceiveWithAnyArgs().GetByOrganizationIdAsync(default);
        await _organizationRepository.DidNotReceiveWithAnyArgs().UpdateSuccessfulOrganizationSyncStatusAsync(default!, default);
    }

    [Theory]
    [InlineData(1001, 0)]
    [InlineData(0, 1001)]
    [InlineData(500, 501)]
    public async Task Refuses_more_than_a_thousand_ids_deleting_nothing(int users, int organizations)
    {
        var request = new JobTriggerRetirementRequestModel
        {
            UserIds = Enumerable.Range(0, users).Select(_ => Guid.NewGuid()).ToList(),
            OrganizationIds = Enumerable.Range(0, organizations).Select(_ => Guid.NewGuid()).ToList(),
        };

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.RetireAsync(request));

        await _emergencyAccessRepository.DidNotReceiveWithAnyArgs().GetManyDetailsByUserIdsAsync(default!);
        await _organizationDomainRepository.DidNotReceiveWithAnyArgs().GetDomainsByOrganizationIdAsync(default);
        await _organizationRepository.DidNotReceiveWithAnyArgs().UpdateSuccessfulOrganizationSyncStatusAsync(default!, default);
    }

    [Fact]
    public async Task Takes_a_thousand_ids()
    {
        var request = new JobTriggerRetirementRequestModel
        {
            UserIds = Enumerable.Range(0, 400).Select(_ => Guid.NewGuid()).ToList(),
            OrganizationIds = Enumerable.Range(0, 600).Select(_ => Guid.NewGuid()).ToList(),
        };

        Assert.Equal(600, Answer(await _sut.RetireAsync(request)).OrganizationsSeatSyncCleared);
    }

    [Fact]
    public async Task Refuses_a_request_without_a_body()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => _sut.RetireAsync(null));
    }

    [Fact]
    public async Task Is_not_there_when_no_client_may_use_it()
    {
        _globalSettings.JobTriggerRetirementClientSecret.Returns((string)null!);

        Assert.IsType<NotFoundResult>(await _sut.RetireAsync(Request()));
        await AssertNothingTouchedAsync();
    }

    [Fact]
    public async Task Is_not_there_without_an_internal_listener()
    {
        _globalSettings.JobTriggerRetirementPort.Returns((int?)null);

        Assert.IsType<NotFoundResult>(await _sut.RetireAsync(Request()));
        await AssertNothingTouchedAsync();
    }

    [Theory]
    [InlineData(33656)]
    [InlineData(0)]
    public async Task Is_not_there_on_any_listener_but_the_internal_one(int port)
    {
        _sut.HttpContext.Connection.LocalPort = port;

        Assert.IsType<NotFoundResult>(await _sut.RetireAsync(Request()));
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

        Assert.IsType<UnauthorizedResult>(await _sut.RetireAsync(Request()));
        await AssertNothingTouchedAsync();
    }

    [Fact]
    public async Task Refuses_a_token_that_does_not_validate()
    {
        Bearer("garbage");
        _tokenValidator.ValidateAccessTokenAsync("garbage", ApiScopes.InternalJobTriggerRetirement)
            .Returns(new TokenValidationResult { IsError = true, Error = "invalid_token" });

        Assert.IsType<UnauthorizedResult>(await _sut.RetireAsync(Request()));
        await AssertNothingTouchedAsync();
    }

    [Fact]
    public async Task Refuses_a_token_that_does_not_validate_for_the_retirement_scope()
    {
        // A deletion client's token, say: it was issued for internal.account-deletion alone.
        Bearer("a-deletion-token");
        _tokenValidator.ValidateAccessTokenAsync("a-deletion-token", ApiScopes.InternalJobTriggerRetirement)
            .Returns(new TokenValidationResult { IsError = true, Error = "insufficient_scope" });

        Assert.IsType<UnauthorizedResult>(await _sut.RetireAsync(Request()));
        await AssertNothingTouchedAsync();
    }

    [Theory]
    [InlineData("web")]
    [InlineData("internal.notifications")]
    [InlineData("deletion.other-service")]
    [InlineData("cohorts.other-service")]
    [InlineData("retire")]
    [InlineData("retiremigration-service")]
    public async Task Refuses_a_token_issued_to_any_other_client(string client)
    {
        Validates(Token, client);

        Assert.IsType<UnauthorizedResult>(await _sut.RetireAsync(Request()));
        await AssertNothingTouchedAsync();
    }

    [Fact]
    public async Task Logs_the_client_and_counts_alone()
    {
        var user = Guid.NewGuid();
        var organization = Guid.NewGuid();
        _emergencyAccessRepository.GetManyDetailsByUserIdsAsync(default!).ReturnsForAnyArgs(
        [
            new EmergencyAccessDetails
            {
                Id = Guid.NewGuid(),
                GrantorId = user,
                Email = "grantee@example.com",
                GrantorEmail = "grantor@example.com",
                KeyEncrypted = "4.a-wrapped-user-key",
            },
        ]);
        _organizationDomainRepository.GetDomainsByOrganizationIdAsync(organization).Returns(
            [new OrganizationDomain { Id = Guid.NewGuid(), OrganizationId = organization, DomainName = "secret.example" }]);

        await _sut.RetireAsync(new JobTriggerRetirementRequestModel { UserIds = [user], OrganizationIds = [organization] });

        var logged = _logger.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(ILogger.Log))
            .Select(call => call.GetArguments()[2]!.ToString()!)
            .ToList();
        Assert.Equal(
        [
            $"Retired job triggers at internal client {Client}'s request: 1 emergency access grants, "
            + "1 domain claims and 0 cohort assignments deleted, 1 organizations' seat sync cleared.",
        ], logged);
        var everything = string.Join("\n", _logger.ReceivedCalls().SelectMany(call => call.GetArguments())
            .Select(argument => argument?.ToString() ?? string.Empty));
        Assert.DoesNotContain("example", everything);
        Assert.DoesNotContain("wrapped", everything);
        Assert.DoesNotContain(user.ToString(), everything);
        Assert.DoesNotContain(organization.ToString(), everything);
    }

    [Fact]
    public void Answers_on_its_internal_route_alone()
    {
        var type = typeof(JobTriggerRetirementController);
        Assert.Equal("internal/job-triggers",
            type.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>().Single().Template);
        Assert.True(type.GetCustomAttributes(typeof(ApiExplorerSettingsAttribute), false)
            .Cast<ApiExplorerSettingsAttribute>().Single().IgnoreApi);
        Assert.Single(type.GetCustomAttributes(typeof(Bit.SharedWeb.Utilities.ExceptionHandlerFilterAttribute), false));
        var post = type.GetMethod(nameof(JobTriggerRetirementController.RetireAsync))!
            .GetCustomAttributes(typeof(HttpPostAttribute), false).Cast<HttpPostAttribute>().Single();
        Assert.Equal("retire", post.Template);
    }

    private static JobTriggerRetirementRequestModel Request() =>
        new() { UserIds = [Guid.NewGuid()], OrganizationIds = [Guid.NewGuid()] };

    private async Task AssertNothingTouchedAsync()
    {
        await _emergencyAccessRepository.DidNotReceiveWithAnyArgs().GetManyDetailsByUserIdsAsync(default!);
        await _emergencyAccessRepository.DidNotReceiveWithAnyArgs().DeleteManyAsync(default!);
        await _organizationDomainRepository.DidNotReceiveWithAnyArgs().GetDomainsByOrganizationIdAsync(default);
        await _organizationDomainRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
        await _cohortAssignmentRepository.DidNotReceiveWithAnyArgs().GetByOrganizationIdAsync(default);
        await _cohortAssignmentRepository.DidNotReceiveWithAnyArgs().DeleteAsync(default!);
        await _organizationRepository.DidNotReceiveWithAnyArgs().UpdateSuccessfulOrganizationSyncStatusAsync(default!, default);
    }

    private static JobTriggerRetirementResponseModel Answer(IActionResult result) =>
        Assert.IsType<JobTriggerRetirementResponseModel>(Assert.IsType<OkObjectResult>(result).Value);

    private void Bearer(string token) =>
        _sut.HttpContext.Request.Headers.Authorization = $"Bearer {token}";

    private void Validates(string token, string client) =>
        _tokenValidator.ValidateAccessTokenAsync(token, ApiScopes.InternalJobTriggerRetirement)
            .Returns(new TokenValidationResult
            {
                IsError = false,
                Claims = [new Claim(JwtClaimTypes.ClientId, client), new Claim(JwtClaimTypes.Scope, ApiScopes.InternalJobTriggerRetirement)],
            });
}
