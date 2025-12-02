using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.Queries.Interfaces;
using Bit.Identity.IdentityServer.RequestValidators;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer.RequestValidators;

public class ClientVersionValidatorTests
{
    private static ICurrentContext MakeContext(Version version)
    {
        var ctx = Substitute.For<ICurrentContext>();
        ctx.ClientVersion = version;
        return ctx;
    }

    private static IGetMinimumClientVersionForUserQuery MakeMinQuery(Version? v)
    {
        var q = Substitute.For<IGetMinimumClientVersionForUserQuery>();
        q.Run(Arg.Any<User>()).Returns(Task.FromResult(v));
        return q;
    }

    [Fact]
    public async Task Allows_When_NoMinVersion()
    {
        var sut = new ClientVersionValidator(MakeContext(new Version("2025.1.0")), MakeMinQuery(null));
        var ok = await sut.ValidateAsync(new User(), new Bit.Identity.IdentityServer.CustomValidatorRequestContext());
        Assert.True(ok);
    }

    [Fact]
    public async Task Blocks_When_ClientTooOld()
    {
        var sut = new ClientVersionValidator(MakeContext(new Version("2025.10.0")), MakeMinQuery(new Version("2025.11.0")));
        var ctx = new Bit.Identity.IdentityServer.CustomValidatorRequestContext();
        var ok = await sut.ValidateAsync(new User(), ctx);
        Assert.False(ok);
        Assert.NotNull(ctx.ValidationErrorResult);
        Assert.True(ctx.ValidationErrorResult.IsError);
        Assert.Equal("invalid_client_version", ctx.ValidationErrorResult.Error);
    }

    [Fact]
    public async Task Allows_When_ClientMeetsMin()
    {
        var sut = new ClientVersionValidator(MakeContext(new Version("2025.11.0")), MakeMinQuery(new Version("2025.11.0")));
        var ok = await sut.ValidateAsync(new User(), new Bit.Identity.IdentityServer.CustomValidatorRequestContext());
        Assert.True(ok);
    }

    [Fact]
    public async Task Allows_When_ClientVersionHeaderMissing()
    {
        // Do not set ClientVersion on the context (remains null) and ensure we fail open
        var ctx = Substitute.For<ICurrentContext>();
        var minQuery = MakeMinQuery(new Version("2025.11.0"));
        var sut = new ClientVersionValidator(ctx, minQuery);

        var ok = await sut.ValidateAsync(new User(), new Bit.Identity.IdentityServer.CustomValidatorRequestContext());

        Assert.True(ok);
    }
}
