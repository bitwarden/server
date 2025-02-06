using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.Identity;

[SutProviderCustomize]
public abstract class BaseTokenProviderTests<T>
    where T : IUserTwoFactorTokenProvider<User>
{
    public abstract TwoFactorProviderType TwoFactorProviderType { get; }

    protected static IEnumerable<object[]> SetupCanGenerateData(params (Dictionary<string, object> MetaData, bool ExpectedResponse)[] data)
    {
        return data.Select(d =>
            new object[]
            {
                d.MetaData,
                d.ExpectedResponse,
            });
    }

    protected virtual IUserService AdditionalSetup(SutProvider<T> sutProvider, User user)
    {
        var userService = Substitute.For<IUserService>();

        sutProvider.GetDependency<IServiceProvider>()
            .GetService(typeof(IUserService))
            .Returns(userService);

        SetupUserService(userService, user);

        return userService;
    }

    protected virtual void SetupUserService(IUserService userService, User user)
    {
        userService
            .TwoFactorProviderIsEnabledAsync(TwoFactorProviderType, user)
            .Returns(true);
        userService
            .CanAccessPremium(user)
            .Returns(true);
    }

    protected static UserManager<User> SubstituteUserManager()
    {
        return new UserManager<User>(Substitute.For<IUserStore<User>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<IPasswordHasher<User>>(),
            Enumerable.Empty<IUserValidator<User>>(),
            Enumerable.Empty<IPasswordValidator<User>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<User>>>());
    }

    protected void MockDatabase(User user, Dictionary<string, object> metaData)
    {
        var providers = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType] = new TwoFactorProvider
            {
                Enabled = true,
                MetaData = metaData,
            },
        };

        user.TwoFactorProviders = JsonHelpers.LegacySerialize(providers);
    }

    public virtual async Task RunCanGenerateTwoFactorTokenAsync(Dictionary<string, object> metaData, bool expectedResponse,
        User user, SutProvider<T> sutProvider)
    {
        var userManager = SubstituteUserManager();
        MockDatabase(user, metaData);

        AdditionalSetup(sutProvider, user);

        var response = await sutProvider.Sut.CanGenerateTwoFactorTokenAsync(userManager, user);
        Assert.Equal(expectedResponse, response);
    }
}
