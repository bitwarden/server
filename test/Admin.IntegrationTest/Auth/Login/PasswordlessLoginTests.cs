using System.Net;
using Bit.IntegrationTestCommon;
using Bit.IntegrationTestCommon.Factories;
using Xunit;

namespace Bit.Admin.IntegrationTest.Auth.Login;

public class PasswordlessLoginTests
{
    [Fact]
    public async Task Confirm_ValidPasswordlessToken_SignsInAdmin()
    {
        ITestDatabase testDatabase = new SqliteTestDatabase();
        try
        {
            await using var factory = new AdminApplicationFactory(testDatabase);
            var client = await factory.SignInAdminAsync();

            // The home page requires an authenticated admin. Without a session the request
            // redirects to the login page, so the final path proves the session is valid.
            var response = await client.GetAsync("/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("/", response.RequestMessage?.RequestUri?.AbsolutePath);
        }
        finally
        {
            testDatabase.Dispose();
        }
    }
}
