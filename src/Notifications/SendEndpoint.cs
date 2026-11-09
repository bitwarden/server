namespace Bit.Notifications;

internal static class SendEndpoint
{
    /// <summary>
    /// Maps <c>POST /send</c>, which a self-hosted installation's API uses to hand notifications directly to
    /// this service in place of the Azure queue.
    /// </summary>
    public static RouteHandlerBuilder MapSendEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/send", async (
                InboundNotification notification,
                HubHelpers hubHelpers,
                HttpContext httpContext) =>
            {
                await hubHelpers.SendNotificationToHubAsync(notification, httpContext.RequestAborted);
                return Results.Ok();
            })
            .RequireAuthorization("Internal");
    }
}
