using System;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Api.Utilities
{
    // Custom class for the requirement
    public class OrganizationApiKeyRequirement : IAuthorizationRequirement
    {
        public OrganizationApiKeyType ApiKeyType { get; }
        public OrganizationApiKeyRequirement(OrganizationApiKeyType apiKeyType)
        {
            ApiKeyType = apiKeyType;
        }
    }

    public class OrganizationApiKeyAuthorizationHandler : AuthorizationHandler<OrganizationApiKeyRequirement>
    {
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OrganizationApiKeyRequirement requirement)
        {
            if (context.Resource is not HttpContext httpContext)
            {
                context.Fail();
                return;
            }

            if (!httpContext.Request.Query.TryGetValue("key", out var keys) || keys.Count != 1)
            {
                context.Fail();
                return;
            }

            // We do have a billing sync key
            var key = keys[0];

            if (!httpContext.Request.Query.TryGetValue("installation", out var installationIds)
                || installationIds.Count != 1
                || !Guid.TryParse(installationIds[0], out var installationId))
            {
                context.Fail();
                return;
            }

            // We have a installation id
            var installationRepository = httpContext.RequestServices.GetRequiredService<IInstallationRepository>();

            var installation = await installationRepository.GetByIdAsync(installationId);

            if (installation == null)
            {
                context.Fail();
                return;
            }

            var tokenFactory = httpContext.RequestServices.GetRequiredService<ISymmetricKeyProtectedTokenFactory<OrganizationApiKeyTokenable>>();

            if (!tokenFactory.TryUnprotect(installation.Key, key, out var token))
            {
                context.Fail();
                return;
            }

            var organizationApiKeyRepository = httpContext.RequestServices.GetRequiredService<IOrganizationApiKeyRepository>();

            if (!await organizationApiKeyRepository.GetCanUseByApiKeyAsync(token.OrganizationId, token.Key, requirement.ApiKeyType))
            {
                context.Fail();
                return;
            }

            httpContext.Features.Set<IApiKeyAuthorizationFeature>(new ApiKeyAuthorizationFeature(installation, token));
            context.Succeed(requirement);
        }
    }
}
