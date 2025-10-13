using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Bit.Core.Models.Mail;
using Bit.Core.Services;
using Bit.Core.Settings;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Rnwood.SmtpServer;
using Rnwood.SmtpServer.Extensions.Auth;
using Xunit.Abstractions;

namespace Bit.Core.IntegrationTest;

public class MailKitSmtpMailDeliveryServiceTests
{
    private static int _loggingConfigured;
    private readonly X509Certificate2 _selfSignedCert;

    public MailKitSmtpMailDeliveryServiceTests(ITestOutputHelper testOutputHelper)
    {
        ConfigureSmtpServerLogging(testOutputHelper);

        _selfSignedCert = CreateSelfSignedCert("localhost");
    }

    private static X509Certificate2 CreateSelfSignedCert(string commonName)
    {
        using var rsa = RSA.Create(2048);
        var certRequest = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return certRequest.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1));
    }

    private static void ConfigureSmtpServerLogging(ITestOutputHelper testOutputHelper)
    {
        // The logging in SmtpServer is configured statically so if we add it for each test it duplicates
        // but we cant add the logger statically either because we need ITestOutputHelper
        if (Interlocked.CompareExchange(ref _loggingConfigured, 1, 0) == 0)
        {
            return;
        }
        // Unfortunately this package doesn't public expose its logging infrastructure
        // so we use private reflection to try and access it. 
        try
        {
            var loggingType = typeof(DefaultServerBehaviour).Assembly.GetType("Rnwood.SmtpServer.Logging")
                ?? throw new Exception("No type found in RnWood.SmtpServer named 'Logging'");

            var factoryProperty = loggingType.GetProperty("Factory")
                ?? throw new Exception($"No property named 'Factory' found on class {loggingType.FullName}");

            var factoryPropertyGet = factoryProperty.GetMethod
                ?? throw new Exception($"{loggingType.FullName}.{factoryProperty.Name} does not have a get method.");

            if (factoryPropertyGet.Invoke(null, null) is not ILoggerFactory loggerFactory)
            {
                throw new Exception($"{loggingType.FullName}.{factoryProperty.Name} is not of type 'ILoggerFactory'" +
                    $"instead it's type '{factoryProperty.PropertyType.FullName}'");
            }

            loggerFactory.AddXUnit(testOutputHelper);
        }
        catch (Exception ex)
        {
            testOutputHelper.WriteLine($"Failed to configure logging for RnWood.SmtpServer (logging will not be configured):\n{ex.Message}");
        }
    }
    private static int RandomPort()
    {
        return Random.Shared.Next(50000, 60000);
    }

    private static GlobalSettings GetSettings(Action<GlobalSettings> configure)
    {
        var globalSettings = new GlobalSettings();
        globalSettings.SiteName = "TestSiteName";
        globalSettings.Mail.ReplyToEmail = "test@example.com";
        globalSettings.Mail.Smtp.Host = "localhost";
        // Set common defaults
        configure(globalSettings);
        return globalSettings;
    }

    [Fact]
    public async Task SendEmailAsync_SmtpServerUsingSelfSignedCert_CertNotInTrustedRootStore_ThrowsException()
    {
        // If an SMTP server is using a self signed cert we currently require
        // that the certificate for their SMTP server is installed in the root CA
        // we are building the ability to do so without installing it, when we add that
        // this test can be copied, and changed to utilize that new feature and instead of
        // failing it should successfully send the email.
        var port = RandomPort();
        var behavior = new DefaultServerBehaviour(false, port, _selfSignedCert);
        using var smtpServer = new SmtpServer(behavior);
        smtpServer.Start();

        var globalSettings = GetSettings(gs =>
        {
            gs.Mail.Smtp.Port = port;
            gs.Mail.Smtp.Ssl = true;
        });

        var mailKitDeliveryService = new MailKitSmtpMailDeliveryService(
            globalSettings,
            NullLogger<MailKitSmtpMailDeliveryService>.Instance
        );

        await Assert.ThrowsAsync<SslHandshakeException>(
            async () => await mailKitDeliveryService.SendEmailAsync(new MailMessage
            {
                Subject = "Test",
                ToEmails = ["test@example.com"],
                TextContent = "Hi",
            }, new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token)
        );
    }

    [Fact]
    public async Task SendEmailAsync_Succeeds_WhenCertIsSelfSigned_ServerIsTrusted()
    {
        // When the setting `TrustServer = true` is set even if the cert is 
        // self signed and the cert is not trusted in anyway the connection should
        // still go through.
        var port = RandomPort();
        var behavior = new DefaultServerBehaviour(false, port, _selfSignedCert);
        using var smtpServer = new SmtpServer(behavior);
        smtpServer.Start();

        var globalSettings = GetSettings(gs =>
        {
            gs.Mail.Smtp.Port = port;
            gs.Mail.Smtp.Ssl = true;
            gs.Mail.Smtp.TrustServer = true;
        });

        var mailKitDeliveryService = new MailKitSmtpMailDeliveryService(
            globalSettings,
            NullLogger<MailKitSmtpMailDeliveryService>.Instance
        );

        var tcs = new TaskCompletionSource();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        cts.Token.Register(() => _ = tcs.TrySetCanceled());

        behavior.MessageReceivedEventHandler += (sender, args) =>
        {
            if (args.Message.Recipients.Contains("test1@example.com"))
            {
                tcs.SetResult();
            }
            return Task.CompletedTask;
        };

        await mailKitDeliveryService.SendEmailAsync(new MailMessage
        {
            Subject = "Test",
            ToEmails = ["test1@example.com"],
            TextContent = "Hi",
        }, cts.Token);

        // Wait for email
        await tcs.Task;
    }

    [Fact]
    public async Task SendEmailAsync_FailsConnectingWithTls_ServerDoesNotSupportTls()
    {
        // If the SMTP server is not setup to use TLS but our server is expecting it
        // to, we should fail.
        var port = RandomPort();
        var behavior = new DefaultServerBehaviour(false, port);
        using var smtpServer = new SmtpServer(behavior);
        smtpServer.Start();

        var globalSettings = GetSettings(gs =>
        {
            gs.Mail.Smtp.Port = port;
            gs.Mail.Smtp.Ssl = true;
            gs.Mail.Smtp.TrustServer = true;
        });

        var mailKitDeliveryService = new MailKitSmtpMailDeliveryService(
            globalSettings,
            NullLogger<MailKitSmtpMailDeliveryService>.Instance
        );

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<SslHandshakeException>(
            async () => await mailKitDeliveryService.SendEmailAsync(new MailMessage
            {
                Subject = "Test",
                ToEmails = ["test1@example.com"],
                TextContent = "Hi",
            }, cts.Token)
        );
    }

    [Fact(Skip = "Requires permission to privileged port")]
    public async Task SendEmailAsync_Works_NoSsl()
    {
        // If the SMTP server isn't set up with any SSL/TLS and we dont' expect
        // any, then the email should go through just fine. Just without encryption.
        // This test has to use port 25
        var port = 25;
        var behavior = new DefaultServerBehaviour(false, port);
        using var smtpServer = new SmtpServer(behavior);
        smtpServer.Start();

        var globalSettings = GetSettings(gs =>
        {
            gs.Mail.Smtp.Port = port;
            gs.Mail.Smtp.Ssl = false;
            gs.Mail.Smtp.StartTls = false;
        });

        var mailKitDeliveryService = new MailKitSmtpMailDeliveryService(
            globalSettings,
            NullLogger<MailKitSmtpMailDeliveryService>.Instance
        );

        var tcs = new TaskCompletionSource();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        cts.Token.Register(() => _ = tcs.TrySetCanceled());

        behavior.MessageReceivedEventHandler += (sender, args) =>
        {
            if (args.Message.Recipients.Contains("test1@example.com"))
            {
                tcs.SetResult();
            }
            return Task.CompletedTask;
        };

        await mailKitDeliveryService.SendEmailAsync(new MailMessage
        {
            Subject = "Test",
            ToEmails = ["test1@example.com"],
            TextContent = "Hi",
        }, cts.Token);

        // Wait for email
        await tcs.Task;
    }

    [Fact]
    public async Task SendEmailAsync_Succeeds_WhenServerNeedsToAuthenticate()
    {
        // When the setting `TrustServer = true` is set even if the cert is 
        // self signed and the cert is not trusted in anyway the connection should
        // still go through.
        var port = RandomPort();
        var behavior = new DefaultServerBehaviour(false, port, _selfSignedCert);
        behavior.AuthenticationCredentialsValidationRequiredEventHandler += (sender, args) =>
        {
            args.AuthenticationResult = AuthenticationResult.Failure;
            if (args.Credentials is not UsernameAndPasswordAuthenticationCredentials usernameAndPasswordCreds)
            {
                return Task.CompletedTask;
            }

            if (usernameAndPasswordCreds.Username != "test" || usernameAndPasswordCreds.Password != "password")
            {
                return Task.CompletedTask;
            }

            args.AuthenticationResult = AuthenticationResult.Success;
            return Task.CompletedTask;
        };
        using var smtpServer = new SmtpServer(behavior);
        smtpServer.Start();

        var globalSettings = GetSettings(gs =>
        {
            gs.Mail.Smtp.Port = port;
            gs.Mail.Smtp.Ssl = true;
            gs.Mail.Smtp.TrustServer = true;

            gs.Mail.Smtp.Username = "test";
            gs.Mail.Smtp.Password = "password";
        });

        var mailKitDeliveryService = new MailKitSmtpMailDeliveryService(
            globalSettings,
            NullLogger<MailKitSmtpMailDeliveryService>.Instance
        );

        var tcs = new TaskCompletionSource();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        cts.Token.Register(() => _ = tcs.TrySetCanceled());

        behavior.MessageReceivedEventHandler += (sender, args) =>
        {
            if (args.Message.Recipients.Contains("test1@example.com"))
            {
                tcs.SetResult();
            }
            return Task.CompletedTask;
        };

        await mailKitDeliveryService.SendEmailAsync(new MailMessage
        {
            Subject = "Test",
            ToEmails = ["test1@example.com"],
            TextContent = "Hi",
        }, cts.Token);

        // Wait for email
        await tcs.Task;
    }
}
