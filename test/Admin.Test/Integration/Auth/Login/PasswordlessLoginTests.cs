using System.Net;
using Admin.Test.Integration.Factories;

namespace Admin.Test.Integration.Auth.Login;

public class PasswordlessLoginTests(AdminApplicationFactory factory) : IClassFixture<AdminApplicationFactory>
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
