using Bit.Api.AdminConsole.Endpoints.Filters;
using Bit.Core.Auth.Identity;
using Bit.ExceptionHandling;

namespace Bit.Api.AdminConsole.Endpoints;

public static class AdminConsoleEndpointConventions
{
    public static RouteGroupBuilder WithAdminConsoleDefaults(this RouteGroupBuilder group, string tag) =>
        group
            .WithTags(tag)
            .WithGroupName("internal")
            .RequireAuthorization(Policies.Application)
            .WithBasicExceptionHandling()
            .AddEndpointFilterFactory(RequestBodyValidationEndpointFilter.Create);
}
