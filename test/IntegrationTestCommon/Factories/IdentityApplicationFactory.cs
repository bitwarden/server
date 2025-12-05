// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Services;
using Bit.Identity;
using Bit.Test.Common.Helpers;
using LinqToDB;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.IntegrationTestCommon.Factories;

public class IdentityApplicationFactory : WebApplicationFactoryBase<Startup>
{
    public const string DefaultDeviceIdentifier = "92b9d953-b9b6-4eaf-9d3e-11d57144dfeb";
    public const string DefaultUserEmail = "DefaultEmail@bitwarden.com";
    public const string DefaultUserPasswordHash = "default_password_hash";

    /// <summary>
    /// A dictionary to store registration tokens for email verification. We cannot substitute the IMailService more than once, so
    /// we capture the email tokens for new user registration in the constructor. The email must be unique otherwise an error will be thrown.
    /// </summary>
    public ConcurrentDictionary<string, string> RegistrationTokens { get; private set; } = new ConcurrentDictionary<string, string>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // This allows us to use the official registration flow
        SubstituteService<IMailService>(service =>
        {
            service.SendRegistrationVerificationEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
                .ReturnsForAnyArgs(Task.CompletedTask)
                .AndDoes(call =>
                {
                    if (!RegistrationTokens.TryAdd(call.ArgAt<string>(0), call.ArgAt<string>(1)))
                    {
                        throw new InvalidOperationException("This email was already registered for new user registration.");
                    }
                });
        });

        base.ConfigureWebHost(builder);
    }

    public async Task<HttpContext> PostRegisterSendEmailVerificationAsync(RegisterSendVerificationEmailRequestModel model)
    {
        return await Server.PostAsync("/accounts/register/send-verification-email", JsonContent.Create(model));
    }

    public async Task<HttpContext> PostRegisterFinishAsync(RegisterFinishRequestModel model)
    {
        return await Server.PostAsync("/accounts/register/finish", JsonContent.Create(model));
    }

    public async Task<HttpContext> PostRegisterVerificationEmailClicked(RegisterVerificationEmailClickedRequestModel model)
    {
        return await Server.PostAsync("/accounts/register/verification-email-clicked", JsonContent.Create(model));
    }

    public async Task<(string Token, string RefreshToken)> TokenFromPasswordAsync(
        string username,
        string password,
        string deviceIdentifier = DefaultDeviceIdentifier,
        string clientId = "web",
        DeviceType deviceType = DeviceType.FirefoxBrowser,
        string deviceName = "firefox")
    {
        var context = await ContextFromPasswordAsync(
            username, password, deviceIdentifier, clientId, deviceType, deviceName);

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        return (root.GetProperty("access_token").GetString(), root.GetProperty("refresh_token").GetString());
    }

    public async Task<HttpContext> ContextFromPasswordAsync(
        string username,
        string password,
        string deviceIdentifier = DefaultDeviceIdentifier,
        string clientId = "web",
        DeviceType deviceType = DeviceType.FirefoxBrowser,
        string deviceName = "firefox")
    {
        var context = await Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", clientId },
            { "deviceType", ((int)deviceType).ToString() },
            { "deviceIdentifier", deviceIdentifier },
            { "deviceName", deviceName },
            { "grant_type", "password" },
            { "username", username },
            { "password", password },
        }));

        return context;
    }

    public async Task<HttpContext> ContextFromPasswordWithTwoFactorAsync(
        string username,
        string password,
        string deviceIdentifier = DefaultDeviceIdentifier,
        string clientId = "web",
        DeviceType deviceType = DeviceType.FirefoxBrowser,
        string deviceName = "firefox",
        string twoFactorProviderType = "Email",
        string twoFactorToken = "two-factor-token")
    {
        var context = await Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "scope", "api offline_access" },
            { "client_id", clientId },
            { "deviceType", ((int)deviceType).ToString() },
            { "deviceIdentifier", deviceIdentifier },
            { "deviceName", deviceName },
            { "grant_type", "password" },
            { "username", username },
            { "password", password },
            { "TwoFactorToken", twoFactorToken },
            { "TwoFactorProvider", twoFactorProviderType },
            { "TwoFactorRemember", "1" },
        }));

        return context;
    }

    public async Task<string> TokenFromAccessTokenAsync(Guid clientId, string clientSecret,
        DeviceType deviceType = DeviceType.SDK)
    {
        var context = await ContextFromAccessTokenAsync(clientId, clientSecret, deviceType);

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        return root.GetProperty("access_token").GetString();
    }

    public async Task<HttpContext> ContextFromAccessTokenAsync(Guid clientId, string clientSecret,
        DeviceType deviceType = DeviceType.SDK)
    {
        var context = await Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api.secrets" },
                { "client_id", clientId.ToString() },
                { "client_secret", clientSecret },
                { "grant_type", "client_credentials" },
                { "deviceType", ((int)deviceType).ToString() }
            }));

        return context;
    }

    public async Task<string> TokenFromOrganizationApiKeyAsync(string clientId, string clientSecret,
        DeviceType deviceType = DeviceType.FirefoxBrowser)
    {
        var context = await ContextFromOrganizationApiKeyAsync(clientId, clientSecret, deviceType);

        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(context);
        var root = body.RootElement;

        return root.GetProperty("access_token").GetString();
    }

    public async Task<HttpContext> ContextFromOrganizationApiKeyAsync(string clientId, string clientSecret,
        DeviceType deviceType = DeviceType.FirefoxBrowser)
    {
        var context = await Server.PostAsync("/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "scope", "api.organization" },
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "grant_type", "client_credentials" },
                { "deviceType", ((int)deviceType).ToString() }
            }));
        return context;
    }

    /// <summary>
    /// Registers a new user to the Identity Application Factory based on the RegisterFinishRequestModel
    /// </summary>
    /// <param name="requestModel">RegisterFinishRequestModel needed to seed data to the test user</param>
    /// <param name="marketingEmails">optional parameter that is tracked during the inital steps of registration.</param>
    /// <returns>returns the newly created user</returns>
    public async Task<User> RegisterNewIdentityFactoryUserAsync(
        RegisterFinishRequestModel requestModel,
        bool marketingEmails = true)
    {
        var sendVerificationEmailReqModel = new RegisterSendVerificationEmailRequestModel
        {
            Email = requestModel.Email,
            Name = "name",
            ReceiveMarketingEmails = marketingEmails
        };

        var sendEmailVerificationResponseHttpContext = await PostRegisterSendEmailVerificationAsync(sendVerificationEmailReqModel);

        Assert.Equal(StatusCodes.Status204NoContent, sendEmailVerificationResponseHttpContext.Response.StatusCode);
        Assert.NotNull(RegistrationTokens[requestModel.Email]);

        // Now we call the finish registration endpoint with the email verification token
        requestModel.EmailVerificationToken = RegistrationTokens[requestModel.Email];

        var postRegisterFinishHttpContext = await PostRegisterFinishAsync(requestModel);

        Assert.Equal(StatusCodes.Status200OK, postRegisterFinishHttpContext.Response.StatusCode);

        var database = GetDatabaseContext();
        var user = await database.Users
            .SingleAsync(u => u.Email == requestModel.Email);

        Assert.NotNull(user);

        return user;
    }
}
