using Bit.Core.Entities;
using Bit.Core.KeyManagement.PasswordHashing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bit.Core.Test.KeyManagement.PasswordHashing;

public class UserPasswordHasherTests
{
    private const string ClientHash = "r5CFRR+n9NQI8a525FY+0BPR0HGOjVJX0cR1KEMnIOo=";

    private readonly IPasswordHasher<User> _sut = CreateHasher();

    [Fact]
    public void HashPassword_ThenVerify_Succeeds()
    {
        var stored = _sut.HashPassword(new User(), ClientHash);

        Assert.Equal(PasswordVerificationResult.Success, _sut.VerifyHashedPassword(new User(), stored, ClientHash));
    }

    [Fact]
    public void VerifyHashedPassword_HashFromPreviousIdentityHasher_Succeeds()
    {
        // The hasher the server registered before this package: Identity V3, 100,000 iterations.
        var previous = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions { IterationCount = 100_000 }));
        var stored = previous.HashPassword(new User(), ClientHash);

        Assert.Equal(PasswordVerificationResult.Success, _sut.VerifyHashedPassword(new User(), stored, ClientHash));
    }

    [Fact]
    public void VerifyHashedPassword_FewerIterations_AsksForRehash()
    {
        var legacy = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions { IterationCount = 25_000 }));
        var stored = legacy.HashPassword(new User(), ClientHash);

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, _sut.VerifyHashedPassword(new User(), stored, ClientHash));
    }

    // As Identity's PasswordHasher<User>, which this replaces: a blank value still hashes.
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void HashPassword_BlankClientHash_Hashes(string clientHash)
    {
        Assert.False(string.IsNullOrEmpty(_sut.HashPassword(new User(), clientHash)));
    }

    [Fact]
    public void VerifyHashedPassword_WrongClientHash_Fails()
    {
        var stored = _sut.HashPassword(new User(), ClientHash);

        Assert.Equal(PasswordVerificationResult.Failed, _sut.VerifyHashedPassword(new User(), stored, "wrong"));
    }

    [Fact]
    public void AddUserPasswordHasher_ReplacesIdentityDefault()
    {
        var services = new ServiceCollection();
        services.AddUserPasswordHasher();
        services.AddIdentityWithoutCookieAuth<User, Role>();

        var hasher = services.BuildServiceProvider().GetRequiredService<IPasswordHasher<User>>();

        Assert.IsType<UserPasswordHasher>(hasher);
    }

    private static IPasswordHasher<User> CreateHasher()
    {
        var services = new ServiceCollection();
        services.AddUserPasswordHasher();
        return services.BuildServiceProvider().GetRequiredService<IPasswordHasher<User>>();
    }
}
