using System.Collections.Specialized;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.Devices.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer;
using Bit.Identity.IdentityServer.RequestValidators;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.RequestValidators;

public class ResourceOwnerPasswordValidatorTests
{
    [Fact]
    public async Task ValidateAsync_InvokesBackfillServiceAtPrelude()
    {
        // Locks in the prelude call site that ensures device-keyed feature flag rollouts
        // bucket reliably for password-grant requests. CurrentContextMiddleware can't see
        // the form-body DeviceIdentifier at /connect/token time, so the back-fill at the
        // top of ValidateAsync is the only mechanism populating it for flag eval downstream.
        //
        // The test short-circuits at the user lookup (next line after the prelude) by
        // throwing, so we don't pay BuildErrorResultAsync's 2-second brute-force delay.
        var userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<User>>(),
            Enumerable.Empty<IUserValidator<User>>(),
            Enumerable.Empty<IPasswordValidator<User>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            NullLogger<UserManager<User>>.Instance);
        userManager.FindByEmailAsync(Arg.Any<string>())
            .Returns<Task<User>>(_ => throw new InvalidOperationException("short-circuit after prelude"));

        var currentContext = Substitute.For<ICurrentContext>();
        var backfillService = Substitute.For<ICurrentContextBackfillService>();

        var sut = new ResourceOwnerPasswordValidator(
            userManager,
            Substitute.For<IUserService>(),
            Substitute.For<IEventService>(),
            Substitute.For<IDeviceValidator>(),
            Substitute.For<ITwoFactorAuthenticationValidator>(),
            Substitute.For<ISsoRequestValidator>(),
            Substitute.For<IOrganizationUserRepository>(),
            new FakeLogger<ResourceOwnerPasswordValidator>(),
            currentContext,
            Substitute.For<GlobalSettings>(),
            Substitute.For<IAuthRequestRepository>(),
            Substitute.For<IUserRepository>(),
            Substitute.For<IFeatureService>(),
            Substitute.For<ISsoConfigRepository>(),
            Substitute.For<IUserDecryptionOptionsBuilder>(),
            Substitute.For<IPolicyRequirementQuery>(),
            Substitute.For<IMailService>(),
            Substitute.For<IUserAccountKeysQuery>(),
            Substitute.For<IClientVersionValidator>(),
            Substitute.For<IUpdateDeviceLastActivityCommand>(),
            backfillService);

        var tokenRequest = new ValidatedTokenRequest { Raw = new NameValueCollection() };
        var context = new ResourceOwnerPasswordValidationContext
        {
            UserName = "user@example.com",
            Password = "password",
            Request = tokenRequest,
        };

        // Act — let the throw propagate; we only care that back-fill ran first.
        try
        {
            await sut.ValidateAsync(context);
        }
        catch (InvalidOperationException)
        {
            // Expected — short-circuit after the back-fill prelude.
        }

        // Assert — prelude call site is wired up.
        backfillService.Received(1).Apply(currentContext, tokenRequest);
    }
}
