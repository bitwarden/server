using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Bit.Api.Utilities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Repositories;
using Bit.Core.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Utilities
{
    public class OrganizationApiKeyAuthorizationHandlerTests
    {
        [Fact]
        public async Task HandleRequirementAsync_Success()
        {
            var installationId = Guid.NewGuid();
            var organizationId = Guid.NewGuid();

            var context = CreateContext((installationRepo, tokenFactory, apiKeyRepo) =>
            {
                installationRepo.GetByIdAsync(installationId)
                    .Returns(new Installation
                    {
                        Id = installationId,
                        Key = "installation_key",
                    });

                tokenFactory.TryUnprotect("installation_key", "test_token", out var value)
                    .Returns(c =>
                    {
                        c[2] = new OrganizationApiKeyTokenable
                        {
                            OrganizationId = organizationId,
                            Key = "sync_key"
                        };

                        return true;
                    });

                apiKeyRepo.GetCanUseByApiKeyAsync(organizationId, "sync_key", OrganizationApiKeyType.BillingSync)
                    .Returns(true);

                return QuickQuery("test_token", installationId);
            });

            var sut = new OrganizationApiKeyAuthorizationHandler();
            await sut.HandleAsync(context);

            Assert.True(context.HasSucceeded);
            var httpContext = Assert.IsAssignableFrom<HttpContext>(context.Resource);
            var apiKeyFeature = httpContext.Features.Get<IApiKeyAuthorizationFeature>();
            Assert.NotNull(apiKeyFeature);
            Assert.NotNull(apiKeyFeature.Installation);
            Assert.NotNull(apiKeyFeature.Token);
            Assert.Equal(installationId, apiKeyFeature.Installation.Id);
            Assert.Equal(organizationId, apiKeyFeature.Token.OrganizationId);
        }

        [Fact]
        public async Task HandleRequirementAsync_BadResource_Fails()
        {
            var context = new AuthorizationHandlerContext(
                new [] { new OrganizationApiKeyRequirement(OrganizationApiKeyType.BillingSync) },
                new ClaimsPrincipal(),
                new { Message = "Not HttpContext" }
            );

            var sut = new OrganizationApiKeyAuthorizationHandler();
            await sut.HandleAsync(context);

            Assert.True(context.HasFailed);
        }

        [Fact]
        public async Task HandleRequirementAsync_MissingKey_Fails()
        {
            var context = CreateContext((_, _, _) =>
            {
                return new Dictionary<string, StringValues>();
            });
            var sut = new OrganizationApiKeyAuthorizationHandler();
            await sut.HandleAsync(context);

            Assert.True(context.HasFailed);
        }

        [Fact]
        public async Task HandleRequirementAsync_MissingInstallation_Fails()
        {
            var context = CreateContext((_, _, _) =>
            {
                return new Dictionary<string, StringValues>
                {
                    ["key"] = "some_key"
                };
            });
            var sut = new OrganizationApiKeyAuthorizationHandler();
            await sut.HandleAsync(context);

            Assert.True(context.HasFailed);
        }

        [Fact]
        public async Task HandleRequirementAsync_BadInstallationId_Fails()
        {
            var context = CreateContext((installRepo, _, _) =>
            {
                var installationId = Guid.NewGuid();
                installRepo.GetByIdAsync(installationId)
                    .Returns(new Installation
                    {
                        Id = installationId,
                        Key = "fake_key"
                    });

                // Create brand new Guid so that it's different from the one we set up
                return QuickQuery("key", Guid.NewGuid());
            });

            var sut = new OrganizationApiKeyAuthorizationHandler();
            await sut.HandleAsync(context);

            Assert.True(context.HasFailed);
        }

        [Fact]
        public async Task HandleRequirementAsync_CanNotUnprotect_Fails()
        {

            var context = CreateContext((installRepo, tokenFactory, _) =>
            {
                var installationId = Guid.NewGuid();

                installRepo.GetByIdAsync(installationId)
                    .Returns(new Installation
                    {
                        Id = installationId,
                        Key = "installation_key",
                    });

                tokenFactory.TryUnprotect("installation_key", "token", out var value)
                    .Returns(c =>
                    {
                        c[2] = default(OrganizationApiKeyTokenable);
                        return false;
                    });

                return QuickQuery("bad_token", installationId);
            });

            var sut = new OrganizationApiKeyAuthorizationHandler();
            await sut.HandleAsync(context);

            Assert.True(context.HasFailed);
        }

        [Fact]
        public async Task HandleRequirementAsync_CannotUseApiKey_Fails()
        {
            var context = CreateContext((installRepo, tokenFactory, apiKeyRepo) =>
            {
                var installationId = Guid.NewGuid();
                installRepo.GetByIdAsync(installationId)
                    .Returns(new Installation
                    {
                        Id = installationId,
                        Key = "installation_key",
                    });

                var organizationId = Guid.NewGuid();
                tokenFactory.TryUnprotect("installation_key", "token", out var value)
                    .Returns(c =>
                    {
                        c[2] = new OrganizationApiKeyTokenable
                        {
                            OrganizationId = organizationId,
                            Key = "apikey"
                        };
                        return true;
                    });

                apiKeyRepo.GetCanUseByApiKeyAsync(organizationId, "apikey", OrganizationApiKeyType.BillingSync)
                    .Returns(false);

                return QuickQuery("token", installationId);
            });

            var sut = new OrganizationApiKeyAuthorizationHandler();
            await sut.HandleAsync(context);

            Assert.True(context.HasFailed);
        }

        private static AuthorizationHandlerContext CreateContext(
            Func<IInstallationRepository, IKeyProtectedTokenFactory<OrganizationApiKeyTokenable>, IOrganizationApiKeyRepository, Dictionary<string, StringValues>> configure)
        {
            var installationRepo = Substitute.For<IInstallationRepository>();
            var tokenFactory = Substitute.For<IKeyProtectedTokenFactory<OrganizationApiKeyTokenable>>();
            var apiKeyRepo = Substitute.For<IOrganizationApiKeyRepository>();

            var serviceProvider = Substitute.For<IServiceProvider>();

            serviceProvider.GetService(typeof(IInstallationRepository))
                .Returns(installationRepo);

            serviceProvider.GetService(typeof(IKeyProtectedTokenFactory<OrganizationApiKeyTokenable>))
                .Returns(tokenFactory);

            serviceProvider.GetService(typeof(IOrganizationApiKeyRepository))
                .Returns(apiKeyRepo);

            var query = configure(installationRepo,
                tokenFactory,
                apiKeyRepo);
            
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Query = new QueryCollection(query);
            httpContext.RequestServices = serviceProvider;


            return new AuthorizationHandlerContext(
                new [] { new OrganizationApiKeyRequirement(OrganizationApiKeyType.BillingSync) },
                new ClaimsPrincipal(),
                httpContext);
        }

        private static Dictionary<string, StringValues> QuickQuery(string key, Guid installationId)
            => new()
            {
                ["key"] = key,
                ["installation"] = installationId.ToString(),
            };
    }
}
