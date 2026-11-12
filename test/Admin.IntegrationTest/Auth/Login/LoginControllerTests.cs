using System.Net;
using Bit.Admin.IntegrationTest.Factories;
using Xunit;

namespace Bit.Admin.IntegrationTest.Auth.Login;

public class LoginControllerTests(AdminApplicationFactory factory) : IClassFixture<AdminApplicationFactory>
{
    [Fact]
    public async Task Confirm_ValidPasswordlessToken_SignsInAdmin()
    {
        var client = await factory.SignInAdminAsync();

        // The home page requires an authenticated admin. Without a session the request
        // redirects to the login page, so the final path proves the session is valid.
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/", response.RequestMessage?.RequestUri?.AbsolutePath);
    }
}
