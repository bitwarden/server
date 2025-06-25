﻿using System.Net.Http.Headers;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.Models.Response;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

public class AccountsControllerTest : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public AccountsControllerTest(ApiApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetAccountsProfile_success()
    {
        var tokens = await _factory.LoginWithNewAccount();
        var client = _factory.CreateClient();

        using var message = new HttpRequestMessage(HttpMethod.Get, "/accounts/profile");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
        var response = await client.SendAsync(message);

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<ProfileResponseModel>();
        Assert.NotNull(content);
        Assert.Equal("integration-test@bitwarden.com", content.Email);
        Assert.NotNull(content.Name);
        Assert.True(content.EmailVerified);
        Assert.False(content.Premium);
        Assert.False(content.PremiumFromOrganization);
        Assert.Equal("en-US", content.Culture);
        Assert.NotNull(content.Key);
        Assert.NotNull(content.PrivateKey);
        Assert.NotNull(content.SecurityStamp);
    }
}
