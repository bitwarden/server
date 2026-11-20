using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Bit.Core;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Organizations.PlanMigration.Commands;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Models.Api;
using Bit.Core.Settings;
using Bit.Identity.Controllers;
using Bit.Identity.Models.Response;
using Bitwarden.Server.Sdk.Features;
using Duende.IdentityModel;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.Controllers;

public class CohortAssignmentSyncControllerTests : IDisposable
{
    private const string Token = "a-token";
    private const string Client = "cohorts.other-service";
    private const int InternalPort = 33657;

    private readonly IBulkSyncCohortAssignmentsCommand _command = Substitute.For<IBulkSyncCohortAssignmentsCommand>();
    private readonly IFeatureService _featureService = Substitute.For<IFeatureService>();
    private readonly ITokenValidator _tokenValidator = Substitute.For<ITokenValidator>();
    private readonly GlobalSettings _globalSettings = Substitute.For<GlobalSettings>();
    private readonly ILogger<CohortAssignmentSyncController> _logger = Substitute.For<ILogger<CohortAssignmentSyncController>>();
    private readonly CohortAssignmentSyncController _sut;

    public CohortAssignmentSyncControllerTests()
    {
        _globalSettings.CohortAssignmentSyncClientSecret.Returns("a-secret");
        _globalSettings.CohortAssignmentSyncPort.Returns(InternalPort);
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(true);
        _command.Run(Arg.Any<IFormFile>()).Returns(new BillingCommandResult<CohortBulkAssignmentResult>(
            new CohortBulkAssignmentResult { Summary = new CohortBulkAssignmentSummary() }));
        _sut = new CohortAssignmentSyncController(_command, _featureService, _tokenValidator, _globalSettings, _logger)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        _sut.HttpContext.Connection.LocalPort = InternalPort;
        Bearer(Token);
        Validates(Token, Client);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task Runs_the_admin_portal_s_own_command_on_the_file_as_given_and_answers_its_summary()
    {
        var file = Csv("OrganizationId,CohortName\n00000000-0000-0000-0000-000000000001,A1\n");
        var summary = new CohortBulkAssignmentSummary { Inserted = 1, Updated = 2, Unassigned = 3, PlanMismatch = 4, Skipped = 5 };
        _command.Run(file).Returns(new BillingCommandResult<CohortBulkAssignmentResult>(new CohortBulkAssignmentResult { Summary = summary }));

        var result = await _sut.BulkSyncAsync(file);

        await _command.Received(1).Run(file);
        var answer = Answer(result);
        Assert.Same(summary, answer.Summary);
        Assert.Empty(answer.Errors);
        Assert.Null(answer.Refusal);
        Assert.Equal(
            """{"object":"cohortAssignmentSync","summary":{"inserted":1,"updated":2,"unassigned":3,"planMismatch":4,"skipped":5},"errors":[],"refusal":null}""",
            Json(result));
    }

    [Fact]
    public async Task Answers_every_line_s_error_in_the_command_s_order_and_words()
    {
        _command.Run(Arg.Any<IFormFile>()).Returns(new BillingCommandResult<CohortBulkAssignmentResult>(new CohortBulkAssignmentResult
        {
            Errors =
            [
                new CohortBulkAssignmentError(4, "Malformed row: expected 2 columns, got 3."),
                new CohortBulkAssignmentError(2, "'x' is not a valid organization id."),
            ],
        }));

        var answer = Answer(await _sut.BulkSyncAsync(Csv("OrganizationId,CohortName\nx,A1\n")));

        Assert.Null(answer.Summary);
        Assert.Null(answer.Refusal);
        Assert.Equal(
            [new CohortAssignmentSyncErrorModel(4, "Malformed row: expected 2 columns, got 3."), new CohortAssignmentSyncErrorModel(2, "'x' is not a valid organization id.")],
            answer.Errors);
    }

    [Fact]
    public async Task Answers_the_command_s_refusal_as_the_page_shows_it()
    {
        _command.Run(Arg.Any<IFormFile>()).Returns(new BillingCommandResult<CohortBulkAssignmentResult>(new Unhandled()));

        var answer = Answer(await _sut.BulkSyncAsync(Csv("OrganizationId,CohortName\n")));

        Assert.Equal("Something went wrong with your request. Please contact support for assistance.", answer.Refusal);
        Assert.Null(answer.Summary);
        Assert.Empty(answer.Errors);
    }

    [Fact]
    public async Task No_file_is_400_in_the_page_s_words()
    {
        var result = await _sut.BulkSyncAsync(null);

        var error = Assert.IsType<ErrorResponseModel>(Assert.IsType<BadRequestObjectResult>(result).Value);
        Assert.Equal("Choose a CSV file to upload.", error.Message);
        await _command.DidNotReceiveWithAnyArgs().Run(default!);
    }

    [Fact]
    public async Task A_file_over_25_MB_is_400_in_the_page_s_words()
    {
        var file = Substitute.For<IFormFile>();
        file.Length.Returns(Constants.FileSize25mb + 1);

        var result = await _sut.BulkSyncAsync(file);

        var error = Assert.IsType<ErrorResponseModel>(Assert.IsType<BadRequestObjectResult>(result).Value);
        Assert.Equal("CSV file exceeds the 25 MB limit.", error.Message);
        await _command.DidNotReceiveWithAnyArgs().Run(default!);
    }

    [Fact]
    public async Task Without_the_upload_s_flag_it_is_404_as_the_page_is()
    {
        _featureService.IsEnabled(FeatureFlagKeys.PM35215_BusinessPlanPriceMigration).Returns(false);

        Assert.IsType<NotFoundResult>(await _sut.BulkSyncAsync(Csv("OrganizationId,CohortName\n")));
        await _command.DidNotReceiveWithAnyArgs().Run(default!);
    }

    [Fact]
    public async Task On_another_port_it_is_404()
    {
        _sut.HttpContext.Connection.LocalPort = 33656;

        Assert.IsType<NotFoundResult>(await _sut.BulkSyncAsync(Csv("OrganizationId,CohortName\n")));
        await _command.DidNotReceiveWithAnyArgs().Run(default!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Without_a_secret_it_is_404(string? secret)
    {
        _globalSettings.CohortAssignmentSyncClientSecret.Returns(secret);

        Assert.IsType<NotFoundResult>(await _sut.BulkSyncAsync(Csv("OrganizationId,CohortName\n")));
    }

    [Fact]
    public async Task Without_a_port_it_is_404()
    {
        _globalSettings.CohortAssignmentSyncPort.Returns((int?)null);

        Assert.IsType<NotFoundResult>(await _sut.BulkSyncAsync(Csv("OrganizationId,CohortName\n")));
    }

    [Fact]
    public async Task Without_a_bearer_token_it_is_401()
    {
        _sut.HttpContext.Request.Headers.Authorization = "Basic abc";

        Assert.IsType<UnauthorizedResult>(await _sut.BulkSyncAsync(Csv("OrganizationId,CohortName\n")));
        await _command.DidNotReceiveWithAnyArgs().Run(default!);
    }

    [Fact]
    public async Task A_token_this_identity_did_not_validate_for_the_sync_s_scope_is_401()
    {
        _tokenValidator.ValidateAccessTokenAsync(Token, ApiScopes.InternalCohortAssignmentSync)
            .Returns(new TokenValidationResult { IsError = true, Error = "invalid_token" });

        Assert.IsType<UnauthorizedResult>(await _sut.BulkSyncAsync(Csv("OrganizationId,CohortName\n")));
    }

    [Theory]
    [InlineData("installation.other-service")]
    [InlineData("organization.other-service")]
    [InlineData("cohorts")]
    [InlineData("web")]
    public async Task Another_client_s_token_is_401(string client)
    {
        Validates(Token, client);

        Assert.IsType<UnauthorizedResult>(await _sut.BulkSyncAsync(Csv("OrganizationId,CohortName\n")));
        await _command.DidNotReceiveWithAnyArgs().Run(default!);
    }

    [Fact]
    public async Task Logs_the_client_and_counts_alone()
    {
        var organization = "00000000-0000-0000-0000-0000000000aa";
        _command.Run(Arg.Any<IFormFile>()).Returns(new BillingCommandResult<CohortBulkAssignmentResult>(new CohortBulkAssignmentResult
        {
            Errors = [new CohortBulkAssignmentError(2, $"Organization {organization} does not exist.")],
        }));

        await _sut.BulkSyncAsync(Csv($"OrganizationId,CohortName\n{organization},Secret cohort\n"));

        var logged = string.Join("\n", _logger.ReceivedCalls().SelectMany(call => call.GetArguments())
            .Select(argument => argument?.ToString() ?? string.Empty));
        Assert.Contains(Client, logged);
        Assert.DoesNotContain(organization, logged);
        Assert.DoesNotContain("Secret cohort", logged);
    }

    private static IFormFile Csv(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", "assignments.csv");
    }

    private static CohortAssignmentSyncResponseModel Answer(IActionResult result) =>
        Assert.IsType<CohortAssignmentSyncResponseModel>(Assert.IsType<OkObjectResult>(result).Value);

    // As MVC writes it: System.Text.Json's web defaults.
    private static string Json(IActionResult result) =>
        JsonSerializer.Serialize(Assert.IsType<OkObjectResult>(result).Value,
            Assert.IsType<OkObjectResult>(result).Value!.GetType(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private void Bearer(string token) =>
        _sut.HttpContext.Request.Headers.Authorization = $"Bearer {token}";

    private void Validates(string token, string client) =>
        _tokenValidator.ValidateAccessTokenAsync(token, ApiScopes.InternalCohortAssignmentSync)
            .Returns(new TokenValidationResult
            {
                IsError = false,
                Claims = [new Claim(JwtClaimTypes.ClientId, client), new Claim(JwtClaimTypes.Scope, ApiScopes.InternalCohortAssignmentSync)],
            });
}
