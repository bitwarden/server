using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Identity.Models.Request.Accounts;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Xunit;
using static Bit.Identity.IntegrationTest.Helpers.AccountsTestHelpers;

namespace Bit.Identity.IntegrationTest.Controllers.Accounts;

public class AccountsControllerPreloginTests
{
    [Theory, BitAutoData]
    public async Task PostPrelogin_WhenUserExistsWithSalt_ReturnsStoredSalt([Required] string name)
    {
        var localFactory = new IdentityApplicationFactory();
        var email = $"test+prelogin+{name}@email.com";
        await CreateUserAsync(email, name, localFactory, masterPasswordSalt: email);

        var context = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = email });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.Equal(email, body.RootElement.GetProperty("salt").GetString());
    }

    [Theory, BitAutoData]
    public async Task PostPrelogin_WhenUserExistsWithNullSalt_ReturnsNullSalt([Required] string name)
    {
        var localFactory = new IdentityApplicationFactory();
        var email = $"test+prelogin+{name}@email.com";
        await CreateUserAsync(email, name, localFactory, masterPasswordSalt: null);

        var context = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = email });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("salt").ValueKind);
    }

    [Theory, BitAutoData]
    public async Task PostPrelogin_WhenUserDoesNotExistAndDefaultHashKeyConfigured_ReturnsDeterministicResult([Required] string name)
    {
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:kdfDefaultHashKey", "test-default-hash-key");
        var email = $"nonexistent+prelogin+{name}@email.com";

        var first = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = email });
        var second = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = email });

        Assert.Equal(StatusCodes.Status200OK, first.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, second.Response.StatusCode);
        using var firstBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(first);
        using var secondBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(second);
        Assert.Equal(firstBody.RootElement.GetProperty("salt").GetRawText(), secondBody.RootElement.GetProperty("salt").GetRawText());
        Assert.Equal(firstBody.RootElement.GetProperty("kdf").GetRawText(), secondBody.RootElement.GetProperty("kdf").GetRawText());
        Assert.Equal(firstBody.RootElement.GetProperty("kdfIterations").GetRawText(), secondBody.RootElement.GetProperty("kdfIterations").GetRawText());
    }

    [Theory, BitAutoData]
    public async Task PostPrelogin_WhenUserDoesNotExistAndNoDefaultHashKey_ReturnsEmailAsSalt([Required] string name)
    {
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:kdfDefaultHashKey", null);
        var email = $"nonexistent+prelogin+{name}@email.com";

        var context = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = email });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        Assert.Equal(email, body.RootElement.GetProperty("salt").GetString());
    }

    [Theory, BitAutoData]
    public async Task PostPrelogin_WhenUserDoesNotExist_ReturnsSaltIndependentOfInputCasing([Required] string name)
    {
        var localFactory = new IdentityApplicationFactory();
        localFactory.UpdateConfiguration("globalSettings:kdfDefaultHashKey", "test-default-hash-key");
        var lowercaseEmail = $"nonexistent+prelogin+{name}@email.com";
        var mixedCaseEmail = lowercaseEmail.ToUpperInvariant();

        var lowercase = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = lowercaseEmail });
        var mixedCase = await localFactory.PostPreloginAsync(new PasswordPreloginRequestModel { Email = mixedCaseEmail });

        Assert.Equal(StatusCodes.Status200OK, lowercase.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, mixedCase.Response.StatusCode);
        using var lowercaseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(lowercase);
        using var mixedCaseBody = await AssertHelper.AssertResponseTypeIs<JsonDocument>(mixedCase);
        Assert.Equal(lowercaseBody.RootElement.GetProperty("salt").GetRawText(), mixedCaseBody.RootElement.GetProperty("salt").GetRawText());
    }
}
