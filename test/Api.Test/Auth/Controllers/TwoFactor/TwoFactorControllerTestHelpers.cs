using Bit.Api.Auth.Controllers;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Tokens;
using Bit.Test.Common.AutoFixture;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Auth.Controllers.TwoFactor;

/// <summary>
/// Shared setup methods, JSON fixtures, and assertions used across the per-provider
/// <c>TwoFactorController</c> unit-test classes.
/// </summary>
internal static class TwoFactorControllerTestHelpers
{
    public static void SetupGetUserByPrincipalAsync(SutProvider<TwoFactorController> sutProvider, User user)
    {
        // Controller actions that call model.ToUser(user) read user.TwoFactorProviders as JSON.
        // BitAutoData populates that with a random non-JSON string; clear it so deserialization
        // doesn't fail before the token validation the test wants to exercise.
        user.TwoFactorProviders = null;

        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(default)
            .ReturnsForAnyArgs(user);
    }

    public static void SetupAuthenticatorTokenFactoryToUnprotectInto(
        SutProvider<TwoFactorController> sutProvider,
        TwoFactorAuthenticatorUserVerificationTokenable tokenable)
    {
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorAuthenticatorUserVerificationTokenable>>()
            .TryUnprotect(Arg.Any<string>(), out Arg.Any<TwoFactorAuthenticatorUserVerificationTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = tokenable;
                return true;
            });
    }

    public static void SetupUserVerificationTokenFactoryToUnprotectInto(
        SutProvider<TwoFactorController> sutProvider,
        TwoFactorUserVerificationTokenable tokenable)
    {
        sutProvider.GetDependency<IDataProtectorTokenFactory<TwoFactorUserVerificationTokenable>>()
            .TryUnprotect(Arg.Any<string>(), out Arg.Any<TwoFactorUserVerificationTokenable>())
            .Returns(callInfo =>
            {
                callInfo[1] = tokenable;
                return true;
            });
    }

    public static TwoFactorUserVerificationTokenable ValidUserVerificationTokenableFor(
        User user, TwoFactorProviderType providerType) =>
        new()
        {
            UserId = user.Id,
            ProviderType = providerType,
            ExpirationDate = DateTime.UtcNow.AddMinutes(30),
        };

    public static void AssertModelStateContains(BadRequestException exception, string key, string expectedMessage)
    {
        Assert.NotNull(exception.ModelState);
        Assert.True(exception.ModelState.ContainsKey(key), $"Expected ModelState to contain key '{key}'.");
        Assert.Contains(exception.ModelState[key]!.Errors, e => e.ErrorMessage == expectedMessage);
    }

    public static string GetUserTwoFactorDuoProvidersJson()
    {
        return
            "{\"2\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }

    public static string GetUserTwoFactorYubiKeyProvidersJson()
    {
        return
            "{\"3\":{\"Enabled\":true,\"MetaData\":{\"Key1\":\"ccccccccccbe\",\"Key2\":null,\"Key3\":null,\"Key4\":null,\"Key5\":null,\"Nfc\":true}}}";
    }

    public static string GetOrganizationTwoFactorDuoProvidersJson()
    {
        return
            "{\"6\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }

    public static void SetupValidateUserBySecretToPass(SutProvider<TwoFactorController> sutProvider, User user)
    {
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(default)
            .ReturnsForAnyArgs(user);

        sutProvider.GetDependency<IUserService>()
            .VerifySecretAsync(default, default)
            .ReturnsForAnyArgs(true);

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(default)
            .ReturnsForAnyArgs(true);
    }

    public static void SetupOrganizationAccessToPass(SutProvider<TwoFactorController> sutProvider, Organization organization)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(true);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(default)
            .ReturnsForAnyArgs(organization);
    }
}
