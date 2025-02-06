
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bit.Identity.Test.Wrappers;

public class UserManagerTestWrapper<TUser> : UserManager<TUser> where TUser : class
{
    /// <summary>
    /// Modify this value to mock the responses from UserManager.GetTwoFactorEnabledAsync()
    /// </summary>
    public bool TWO_FACTOR_ENABLED { get; set; } = false;
    /// <summary>
    /// Modify this value to mock the responses from UserManager.GetValidTwoFactorProvidersAsync()
    /// </summary>
    public IList<string> TWO_FACTOR_PROVIDERS { get; set; } = [];
    /// <summary>
    /// Modify this value to mock the responses from UserManager.GenerateTwoFactorTokenAsync()
    /// </summary>
    public string TWO_FACTOR_TOKEN { get; set; } = string.Empty;
    /// <summary>
    /// Modify this value to mock the responses from UserManager.VerifyTwoFactorTokenAsync()
    /// </summary>
    public bool TWO_FACTOR_TOKEN_VERIFIED { get; set; } = false;

    /// <summary>
    /// Modify this value to mock the responses from UserManager.SupportsUserTwoFactor
    /// </summary>
    public bool SUPPORTS_TWO_FACTOR { get; set; } = false;

    public override bool SupportsUserTwoFactor
    {
        get
        {
            return SUPPORTS_TWO_FACTOR;
        }
    }

    public UserManagerTestWrapper(
        IUserStore<TUser> store,
        IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<TUser> passwordHasher,
        IEnumerable<IUserValidator<TUser>> userValidators,
        IEnumerable<IPasswordValidator<TUser>> passwordValidators,
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        ILogger<UserManager<TUser>> logger)
        : base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators,
            keyNormalizer, errors, services, logger)
    { }

    /// <summary>
    /// return class variable TWO_FACTOR_ENABLED
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public override async Task<bool> GetTwoFactorEnabledAsync(TUser user)
    {
        return TWO_FACTOR_ENABLED;
    }

    /// <summary>
    /// return class variable TWO_FACTOR_PROVIDERS
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public override async Task<IList<string>> GetValidTwoFactorProvidersAsync(TUser user)
    {
        return TWO_FACTOR_PROVIDERS;
    }

    /// <summary>
    /// return class variable TWO_FACTOR_TOKEN
    /// </summary>
    /// <param name="user"></param>
    /// <param name="tokenProvider"></param>
    /// <returns></returns>
    public override async Task<string> GenerateTwoFactorTokenAsync(TUser user, string tokenProvider)
    {
        return TWO_FACTOR_TOKEN;
    }

    /// <summary>
    /// return class variable TWO_FACTOR_TOKEN_VERIFIED
    /// </summary>
    /// <param name="user"></param>
    /// <param name="tokenProvider"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public override async Task<bool> VerifyTwoFactorTokenAsync(TUser user, string tokenProvider, string token)
    {
        return TWO_FACTOR_TOKEN_VERIFIED;
    }
}
