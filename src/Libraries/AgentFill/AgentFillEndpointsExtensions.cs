using Bit.AgentFill.Commands;
using Bit.AgentFill.Models;
using Bit.AgentFill.Queries;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.ExceptionHandling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Bit.AgentFill;

/// <summary>Maps the agent fill approval HTTP surface as a Minimal API endpoint group.</summary>
public static class AgentFillEndpointsExtensions
{
    /// <summary>
    /// Maps create, read and answer for agent fill approval requests on an empty group; the host owns the route
    /// prefix. Every endpoint requires an authenticated user and the
    /// <see cref="AgentFillFeatureFlags.AgentFillApprovals"/> flag, and serves only records the caller owns: any
    /// other record returns <c>404</c>.
    /// </summary>
    public static RouteGroupBuilder MapAgentFillEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("");
        group.WithTags("AgentFill");
        group.WithGroupName("internal");
        group.RequireAuthorization(Policies.Application);
        group.WithBasicExceptionHandling();
        group.RequireFeature(AgentFillFeatureFlags.AgentFillApprovals);

        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });

        group.MapPost("/", CreateAsync)
            .WithName("CreateAgentFillApprovalRequest")
            .WithDescription("Stores a sealed agent fill approval request and notifies the user's mobile devices.");

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("GetAgentFillApprovalRequest")
            .WithDescription("Returns an agent fill approval request owned by the caller.");

        group.MapPut("/{id:guid}", AnswerAsync)
            .WithName("AnswerAgentFillApprovalRequest")
            .WithDescription("Records the first sealed response to an agent fill approval request.");

        return group;
    }

    internal static async Task<Ok<ApprovalRecordResponseModel>> CreateAsync(
        [FromBody] CreateApprovalRequestRequest request,
        [FromServices] ICurrentContext currentContext,
        [FromServices] IDeviceRepository deviceRepository,
        [FromServices] ICreateApprovalRequestCommand command,
        [FromServices] TimeProvider timeProvider)
    {
        var sealedRequest = SealedField.Validate(request.SealedRequest, nameof(request.SealedRequest));
        var (userId, deviceId) = await GetCallerAsync(currentContext, deviceRepository);

        var created = await command.CreateAsync(userId, deviceId, sealedRequest);
        return TypedResults.Ok(ApprovalRecordResponseModel.From(created, timeProvider.GetUtcNow().UtcDateTime));
    }

    internal static async Task<Results<Ok<ApprovalRecordResponseModel>, NotFound>> GetAsync(
        Guid id,
        [FromServices] ICurrentContext currentContext,
        [FromServices] IGetApprovalRequestQuery query,
        [FromServices] TimeProvider timeProvider)
    {
        var userId = GetUserId(currentContext);
        var record = await query.GetAsync(id, userId);
        if (record is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(ApprovalRecordResponseModel.From(record, timeProvider.GetUtcNow().UtcDateTime));
    }

    internal static async Task<Results<Ok<ApprovalRecordResponseModel>, NotFound, Conflict<ApprovalRecordResponseModel>, StatusCodeHttpResult>> AnswerAsync(
        Guid id,
        [FromBody] AnswerApprovalRequestRequest request,
        [FromServices] ICurrentContext currentContext,
        [FromServices] IDeviceRepository deviceRepository,
        [FromServices] IAnswerApprovalRequestCommand command,
        [FromServices] TimeProvider timeProvider)
    {
        var sealedResponse = SealedField.Validate(request.SealedResponse, nameof(request.SealedResponse));
        var (userId, deviceId) = await GetCallerAsync(currentContext, deviceRepository);

        var result = await command.AnswerAsync(id, userId, deviceId, sealedResponse);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        return result.Outcome switch
        {
            AnswerOutcome.Answered => TypedResults.Ok(ApprovalRecordResponseModel.From(result.Request!, now)),
            AnswerOutcome.AlreadyAnswered => TypedResults.Conflict(ApprovalRecordResponseModel.From(result.Request!, now)),
            AnswerOutcome.Expired => TypedResults.StatusCode(StatusCodes.Status410Gone),
            _ => TypedResults.NotFound(),
        };
    }

    private static Guid GetUserId(ICurrentContext currentContext)
        // The Application policy guarantees an authenticated user; this only guards against a misconfigured host.
        => currentContext.UserId ?? throw new UnauthorizedAccessException();

    /// <summary>Resolves the calling user and their registered device. An unknown device is a <c>400</c>.</summary>
    private static async Task<(Guid UserId, Guid DeviceId)> GetCallerAsync(
        ICurrentContext currentContext, IDeviceRepository deviceRepository)
    {
        var userId = GetUserId(currentContext);
        if (string.IsNullOrWhiteSpace(currentContext.DeviceIdentifier))
        {
            throw new BadRequestException("Device is unknown.");
        }

        var device = await deviceRepository.GetByIdentifierAsync(currentContext.DeviceIdentifier, userId)
            ?? throw new BadRequestException("Device is unknown.");

        return (userId, device.Id);
    }
}
