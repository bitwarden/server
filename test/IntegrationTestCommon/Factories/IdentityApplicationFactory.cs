// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bit.Core;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Core.Services;
using Bit.Identity;
using Bit.Identity.IdentityServer;
using Bit.Identity.IdentityServer.RequestValidators;
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
    private const string DefaultEncryptedString = "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";
    public bool UseMockClientVersionValidator { get; set; } = true;

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

        if (UseMockClientVersionValidator)
        {
            // Bypass client version gating to isolate tests from client version behavior
            SubstituteService<IClientVersionValidator>(svc =>
            {
                svc.Validate(Arg.Any<User>(), Arg.Any<CustomValidatorRequestContext>())
                    .Returns(true);
            });
        }

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
    /// <param name="marketingEmails">optional parameter that is tracked during the initial steps of registration.</param>
    /// <returns>returns the newly created user</returns>
    public async Task<User> RegisterNewIdentityFactoryUserAsync(
        RegisterFinishRequestModel requestModel,
        bool marketingEmails = true)
    {
        // Ensure required fields for registration finish are present.
        // Prefer legacy-path defaults (root fields) to minimize changes to tests.
        // PM-28143 - When MasterPasswordAuthenticationData is required, delete all handling of MasterPasswordHash.
        requestModel.MasterPasswordHash ??= DefaultUserPasswordHash;
        // PM-28143 - When KDF is sourced exclusively from MasterPasswordUnlockData, delete the root Kdf defaults below.
        requestModel.Kdf ??= KdfType.PBKDF2_SHA256;
        requestModel.KdfIterations ??= AuthConstants.PBKDF2_ITERATIONS.Default;
        // Ensure a symmetric key is provided when no unlock data is present
        // PM-28143 - When MasterPasswordUnlockData is required, delete the UserSymmetricKey fallback block below.
        if (requestModel.MasterPasswordUnlock == null && string.IsNullOrWhiteSpace(requestModel.UserSymmetricKey))
        {
            requestModel.UserSymmetricKey = "user_symmetric_key";
        }

        // Align unlock/auth data KDF with root KDF so login uses the provided master password hash.
        // PM-28143 - After removing root Kdf fields, build KDF exclusively from MasterPasswordUnlockData.Kdf and delete this alignment section.
        var effectiveKdfType = requestModel.Kdf ?? KdfType.PBKDF2_SHA256;
        var effectiveIterations = requestModel.KdfIterations ?? AuthConstants.PBKDF2_ITERATIONS.Default;
        int? effectiveMemory = null;
        int? effectiveParallelism = null;
        if (effectiveKdfType == KdfType.Argon2id)
        {
            effectiveIterations = AuthConstants.ARGON2_ITERATIONS.InsideRange(effectiveIterations)
                ? effectiveIterations
                : AuthConstants.ARGON2_ITERATIONS.Default;
            effectiveMemory = AuthConstants.ARGON2_MEMORY.Default;
            effectiveParallelism = AuthConstants.ARGON2_PARALLELISM.Default;
        }

        var alignedKdf = new KdfRequestModel
        {
            KdfType = effectiveKdfType,
            Iterations = effectiveIterations,
            Memory = effectiveMemory,
            Parallelism = effectiveParallelism
        };

        if (requestModel.MasterPasswordUnlock != null)
        {
            var unlock = requestModel.MasterPasswordUnlock;
            // Always force a valid encrypted string for tests to avoid model validation failures.
            requestModel.MasterPasswordUnlock = new MasterPasswordUnlockDataRequestModel
            {
                Kdf = alignedKdf,
                MasterKeyWrappedUserKey = unlock.MasterKeyWrappedUserKey,
                Salt = string.IsNullOrWhiteSpace(unlock.Salt) ? requestModel.Email : unlock.Salt
            };
        }

        if (requestModel.MasterPasswordAuthentication != null)
        {
            // Ensure registration uses the same hash the tests will provide at login.
            // PM-28143 - When MasterPasswordAuthenticationData is the only source of the auth hash,
            // stop overriding it from MasterPasswordHash and delete this whole reassignment block.
            requestModel.MasterPasswordAuthentication = new MasterPasswordAuthenticationDataRequestModel
            {
                Kdf = alignedKdf,
                MasterPasswordAuthenticationHash = requestModel.MasterPasswordHash,
                Salt = requestModel.Email
            };
        }

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
        if (postRegisterFinishHttpContext.Response.StatusCode != StatusCodes.Status200OK)
        {
            var body = await ReadResponseBodyAsync(postRegisterFinishHttpContext);
            Assert.Fail($"register/finish failed (status {postRegisterFinishHttpContext.Response.StatusCode}). Body: {body}");
        }

        var database = GetDatabaseContext();
        var user = await database.Users
            .SingleAsync(u => u.Email == requestModel.Email);

        Assert.NotNull(user);

        return user;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext ctx)
    {
        try
        {
            if (ctx?.Response.Body == null)
            {
                return "<no body>";
            }
            var stream = ctx.Response.Body;
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var text = await reader.ReadToEndAsync();
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
            }
            return string.IsNullOrWhiteSpace(text) ? "<empty body>" : text;
        }
        catch (Exception ex)
        {
            return $"<error reading body: {ex.Message}>";
        }
    }

}
