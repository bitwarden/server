using System.Text;
using Bit.Billing.Controllers;
using Bit.Billing.Test.Utilities;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Divergic.Logging.Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;
using Xunit.Abstractions;
using Transaction = Bit.Core.Entities.Transaction;

namespace Bit.Billing.Test.Controllers;

public class PayPalControllerTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    private readonly IOptions<BillingSettings> _billingSettings = Substitute.For<IOptions<BillingSettings>>();
    private readonly IMailService _mailService = Substitute.For<IMailService>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IPaymentService _paymentService = Substitute.For<IPaymentService>();
    private readonly ITransactionRepository _transactionRepository = Substitute.For<ITransactionRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IProviderRepository _providerRepository = Substitute.For<IProviderRepository>();

    private const string _defaultWebhookKey = "webhook-key";

    public PayPalControllerTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task PostIpn_NullKey_BadRequest()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        var controller = ConfigureControllerContextWith(logger, null, null);

        var result = await controller.PostIpn();

        HasStatusCode(result, 400);

        LoggedError(logger, "PayPal IPN: Key is missing");
    }

    [Fact]
    public async Task PostIpn_IncorrectKey_BadRequest()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        _billingSettings.Value.Returns(new BillingSettings
        {
            PayPal = { WebhookKey = "INCORRECT" }
        });

        var controller = ConfigureControllerContextWith(logger, _defaultWebhookKey, null);

        var result = await controller.PostIpn();

        HasStatusCode(result, 400);

        LoggedError(logger, "PayPal IPN: Key is incorrect");
    }

    [Fact]
    public async Task PostIpn_EmptyIPNBody_BadRequest()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        _billingSettings.Value.Returns(new BillingSettings
        {
            PayPal = { WebhookKey = _defaultWebhookKey }
        });

        var controller = ConfigureControllerContextWith(logger, _defaultWebhookKey, null);

        var result = await controller.PostIpn();

        HasStatusCode(result, 400);

        LoggedError(logger, "PayPal IPN: Request body is null or empty");
    }

    [Fact]
    public async Task PostIpn_IPNHasNoEntityId_BadRequest()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        _billingSettings.Value.Returns(new BillingSettings
        {
            PayPal = { WebhookKey = _defaultWebhookKey }
        });

        var ipnBody = await PayPalTestIPN.GetAsync(IPNBody.TransactionMissingEntityIds);

        var controller = ConfigureControllerContextWith(logger, _defaultWebhookKey, ipnBody);

        var result = await controller.PostIpn();

        HasStatusCode(result, 400);

        LoggedError(logger, "PayPal IPN (2PK15573S8089712Y): 'custom' did not contain a User ID or Organization ID or provider ID");
    }

    [Fact]
    public async Task PostIpn_OtherTransactionType_Unprocessed_Ok()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        _billingSettings.Value.Returns(new BillingSettings
        {
            PayPal = { WebhookKey = _defaultWebhookKey }
        });

        var organizationId = new Guid("ca8c6f2b-2d7b-4639-809f-b0e5013a304e");

        var ipnBody = await PayPalTestIPN.GetAsync(IPNBody.UnsupportedTransactionType);

        var controller = ConfigureControllerContextWith(logger, _defaultWebhookKey, ipnBody);

        var result = await controller.PostIpn();

        HasStatusCode(result, 200);

        LoggedWarning(logger, "PayPal IPN (2PK15573S8089712Y): Transaction type (other) not supported for payments");
    }

    [Fact]
    public async Task PostIpn_MismatchedReceiverID_Unprocessed_Ok()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        _billingSettings.Value.Returns(new BillingSettings
        {
            PayPal =
            {
                WebhookKey = _defaultWebhookKey,
                BusinessId = "INCORRECT"
            }
        });

        var organizationId = new Guid("ca8c6f2b-2d7b-4639-809f-b0e5013a304e");

        var ipnBody = await PayPalTestIPN.GetAsync(IPNBody.SuccessfulPayment);

        var controller = ConfigureControllerContextWith(logger, _defaultWebhookKey, ipnBody);

        var result = await controller.PostIpn();

        HasStatusCode(result, 200);

        LoggedWarning(logger, "PayPal IPN (2PK15573S8089712Y): Receiver ID (NHDYKLQ3L4LWL) does not match Bitwarden business ID (INCORRECT)");
    }

    [Fact]
    public async Task PostIpn_RefundMissingParent_Unprocessed_Ok()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        _billingSettings.Value.Returns(new BillingSettings
        {
            PayPal =
            {
                WebhookKey = _defaultWebhookKey,
                BusinessId = "NHDYKLQ3L4LWL"
            }
        });

        var organizationId = new Guid("ca8c6f2b-2d7b-4639-809f-b0e5013a304e");

        var ipnBody = await PayPalTestIPN.GetAsync(IPNBody.RefundMissingParentTransaction);

        var controller = ConfigureControllerContextWith(logger, _defaultWebhookKey, ipnBody);

        var result = await controller.PostIpn();

        HasStatusCode(result, 200);

        LoggedWarning(logger, "PayPal IPN (2PK15573S8089712Y): Parent transaction ID is required for refund");
    }

    [Fact]
    public async Task PostIpn_eCheckPayment_Unprocessed_Ok()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        _billingSettings.Value.Returns(new BillingSettings
        {
            PayPal =
            {
                WebhookKey = _defaultWebhookKey,
                BusinessId = "NHDYKLQ3L4LWL"
            }
        });

        var organizationId = new Guid("ca8c6f2b-2d7b-4639-809f-b0e5013a304e");

        var ipnBody = await PayPalTestIPN.GetAsync(IPNBody.ECheckPayment);

        var controller = ConfigureControllerContextWith(logger, _defaultWebhookKey, ipnBody);

        var result = await controller.PostIpn();

        HasStatusCode(result, 200);

        LoggedWarning(logger, "PayPal IPN (2PK15573S8089712Y): Transaction was an eCheck payment");
    }

    [Fact]
    public async Task PostIpn_NonUSD_Unprocessed_Ok()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        _billingSettings.Value.Returns(new BillingSettings
        {
            PayPal =
            {
                WebhookKey = _defaultWebhookKey,
                BusinessId = "NHDYKLQ3L4LWL"
            }
        });

        var organizationId = new Guid("ca8c6f2b-2d7b-4639-809f-b0e5013a304e");

        var ipnBody = await PayPalTestIPN.GetAsync(IPNBody.NonUSDPayment);

        var controller = ConfigureControllerContextWith(logger, _defaultWebhookKey, ipnBody);

        var result = await controller.PostIpn();

        HasStatusCode(result, 200);

        LoggedWarning(logger, "PayPal IPN (2PK15573S8089712Y): Transaction was not in USD (CAD)");
    }

    [Fact]
    public async Task PostIpn_Completed_ExistingTransaction_Unprocessed_Ok()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        _billingSettings.Value.Returns(new BillingSettings
        {
            PayPal =
            {
                WebhookKey = _defaultWebhookKey,
                BusinessId = "NHDYKLQ3L4LWL"
            }
        });

        var organizationId = new Guid("ca8c6f2b-2d7b-4639-809f-b0e5013a304e");

        var ipnBody = await PayPalTestIPN.GetAsync(IPNBody.SuccessfulPayment);

        _transactionRepository.GetByGatewayIdAsync(
            GatewayType.PayPal,
            "2PK15573S8089712Y").Returns(new Transaction());

        var controller = ConfigureControllerContextWith(logger, _defaultWebhookKey, ipnBody);

        var result = await controller.PostIpn();

        HasStatusCode(result, 200);

        LoggedWarning(logger, "PayPal IPN (2PK15573S8089712Y): Already processed this completed transaction");
    }

    [Fact]
    public async Task PostIpn_Completed_CreatesTransaction_Ok()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        _billingSettings.Value.Returns(new BillingSettings
        {
            PayPal =
            {
                WebhookKey = _defaultWebhookKey,
                BusinessId = "NHDYKLQ3L4LWL"
            }
        });

        var organizationId = new Guid("ca8c6f2b-2d7b-4639-809f-b0e5013a304e");

        var ipnBody = await PayPalTestIPN.GetAsync(IPNBody.SuccessfulPayment);

        _transactionRepository.GetByGatewayIdAsync(
            GatewayType.PayPal,
            "2PK15573S8089712Y").ReturnsNull();

        var controller = ConfigureControllerContextWith(logger, _defaultWebhookKey, ipnBody);

        var result = await controller.PostIpn();

        HasStatusCode(result, 200);

        await _transactionRepository.Received().CreateAsync(Arg.Any<Transaction>());

        await _paymentService.DidNotReceiveWithAnyArgs().CreditAccountAsync(Arg.Any<ISubscriber>(), Arg.Any<decimal>());
    }

    [Fact]
    public async Task PostIpn_Completed_CreatesTransaction_CreditsOrganizationAccount_Ok()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        _billingSettings.Value.Returns(new BillingSettings
        {
            PayPal =
            {
                WebhookKey = _defaultWebhookKey,
                BusinessId = "NHDYKLQ3L4LWL"
            }
        });

        var organizationId = new Guid("ca8c6f2b-2d7b-4639-809f-b0e5013a304e");

        var ipnBody = await PayPalTestIPN.GetAsync(IPNBody.SuccessfulPaymentForOrganizationCredit);

        _transactionRepository.GetByGatewayIdAsync(
            GatewayType.PayPal,
            "2PK15573S8089712Y").ReturnsNull();

        const string billingEmail = "billing@organization.com";

        var organization = new Organization { BillingEmail = billingEmail };

        _organizationRepository.GetByIdAsync(organizationId).Returns(organization);

        _paymentService.CreditAccountAsync(organization, 48M).Returns(true);

        var controller = ConfigureControllerContextWith(logger, _defaultWebhookKey, ipnBody);

        var result = await controller.PostIpn();

        HasStatusCode(result, 200);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(transaction =>
            transaction.GatewayId == "2PK15573S8089712Y" &&
            transaction.OrganizationId == organizationId &&
            transaction.Amount == 48M));

        await _paymentService.Received(1).CreditAccountAsync(organization, 48M);

        await _organizationRepository.Received(1).ReplaceAsync(organization);

        await _mailService.Received(1).SendAddedCreditAsync(billingEmail, 48M);
    }

    [Fact]
    public async Task PostIpn_Completed_CreatesTransaction_CreditsUserAccount_Ok()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        _billingSettings.Value.Returns(new BillingSettings
        {
            PayPal =
            {
                WebhookKey = _defaultWebhookKey,
                BusinessId = "NHDYKLQ3L4LWL"
            }
        });

        var userId = new Guid("ca8c6f2b-2d7b-4639-809f-b0e5013a304e");

        var ipnBody = await PayPalTestIPN.GetAsync(IPNBody.SuccessfulPaymentForUserCredit);

        _transactionRepository.GetByGatewayIdAsync(
            GatewayType.PayPal,
            "2PK15573S8089712Y").ReturnsNull();

        const string billingEmail = "billing@user.com";

        var user = new User { Email = billingEmail };

        _userRepository.GetByIdAsync(userId).Returns(user);

        _paymentService.CreditAccountAsync(user, 48M).Returns(true);

        var controller = ConfigureControllerContextWith(logger, _defaultWebhookKey, ipnBody);

        var result = await controller.PostIpn();

        HasStatusCode(result, 200);

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(transaction =>
            transaction.GatewayId == "2PK15573S8089712Y" &&
            transaction.UserId == userId &&
            transaction.Amount == 48M));

        await _paymentService.Received(1).CreditAccountAsync(user, 48M);

        await _userRepository.Received(1).ReplaceAsync(user);

        await _mailService.Received(1).SendAddedCreditAsync(billingEmail, 48M);
    }

    [Fact]
    public async Task PostIpn_Refunded_ExistingTransaction_Unprocessed_Ok()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        _billingSettings.Value.Returns(new BillingSettings
        {
            PayPal =
            {
                WebhookKey = _defaultWebhookKey,
                BusinessId = "NHDYKLQ3L4LWL"
            }
        });

        var organizationId = new Guid("ca8c6f2b-2d7b-4639-809f-b0e5013a304e");

        var ipnBody = await PayPalTestIPN.GetAsync(IPNBody.SuccessfulRefund);

        _transactionRepository.GetByGatewayIdAsync(
            GatewayType.PayPal,
            "2PK15573S8089712Y").Returns(new Transaction());

        var controller = ConfigureControllerContextWith(logger, _defaultWebhookKey, ipnBody);

        var result = await controller.PostIpn();

        HasStatusCode(result, 200);

        LoggedWarning(logger, "PayPal IPN (2PK15573S8089712Y): Already processed this refunded transaction");

        await _transactionRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(Arg.Any<Transaction>());

        await _transactionRepository.DidNotReceiveWithAnyArgs().CreateAsync(Arg.Any<Transaction>());
    }

    [Fact]
    public async Task PostIpn_Refunded_MissingParentTransaction_Ok()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        _billingSettings.Value.Returns(new BillingSettings
        {
            PayPal =
            {
                WebhookKey = _defaultWebhookKey,
                BusinessId = "NHDYKLQ3L4LWL"
            }
        });

        var organizationId = new Guid("ca8c6f2b-2d7b-4639-809f-b0e5013a304e");

        var ipnBody = await PayPalTestIPN.GetAsync(IPNBody.SuccessfulRefund);

        _transactionRepository.GetByGatewayIdAsync(
            GatewayType.PayPal,
            "2PK15573S8089712Y").ReturnsNull();

        _transactionRepository.GetByGatewayIdAsync(
            GatewayType.PayPal,
            "PARENT").ReturnsNull();

        var controller = ConfigureControllerContextWith(logger, _defaultWebhookKey, ipnBody);

        var result = await controller.PostIpn();

        HasStatusCode(result, 200);

        LoggedWarning(logger, "PayPal IPN (2PK15573S8089712Y): Could not find parent transaction");

        await _transactionRepository.DidNotReceiveWithAnyArgs().ReplaceAsync(Arg.Any<Transaction>());

        await _transactionRepository.DidNotReceiveWithAnyArgs().CreateAsync(Arg.Any<Transaction>());
    }

    [Fact]
    public async Task PostIpn_Refunded_ReplacesParent_CreatesTransaction_Ok()
    {
        var logger = _testOutputHelper.BuildLoggerFor<PayPalController>();

        _billingSettings.Value.Returns(new BillingSettings
        {
            PayPal =
            {
                WebhookKey = _defaultWebhookKey,
                BusinessId = "NHDYKLQ3L4LWL"
            }
        });

        var organizationId = new Guid("ca8c6f2b-2d7b-4639-809f-b0e5013a304e");

        var ipnBody = await PayPalTestIPN.GetAsync(IPNBody.SuccessfulRefund);

        _transactionRepository.GetByGatewayIdAsync(
            GatewayType.PayPal,
            "2PK15573S8089712Y").ReturnsNull();

        var parentTransaction = new Transaction
        {
            GatewayId = "PARENT",
            Amount = 48M,
            RefundedAmount = 0,
            Refunded = false
        };

        _transactionRepository.GetByGatewayIdAsync(
            GatewayType.PayPal,
            "PARENT").Returns(parentTransaction);

        var controller = ConfigureControllerContextWith(logger, _defaultWebhookKey, ipnBody);

        var result = await controller.PostIpn();

        HasStatusCode(result, 200);

        await _transactionRepository.Received(1).ReplaceAsync(Arg.Is<Transaction>(transaction =>
            transaction.GatewayId == "PARENT" &&
            transaction.RefundedAmount == 48M &&
            transaction.Refunded == true));

        await _transactionRepository.Received(1).CreateAsync(Arg.Is<Transaction>(transaction =>
            transaction.GatewayId == "2PK15573S8089712Y" &&
            transaction.Amount == 48M &&
            transaction.OrganizationId == organizationId &&
            transaction.Type == TransactionType.Refund));
    }

    private PayPalController ConfigureControllerContextWith(
        ILogger<PayPalController> logger,
        string webhookKey,
        string ipnBody)
    {
        var controller = new PayPalController(
            _billingSettings,
            logger,
            _mailService,
            _organizationRepository,
            _paymentService,
            _transactionRepository,
            _userRepository,
            _providerRepository);

        var httpContext = new DefaultHttpContext();

        if (!string.IsNullOrEmpty(webhookKey))
        {
            httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
            {
                { "key", new StringValues(webhookKey) }
            });
        }

        if (!string.IsNullOrEmpty(ipnBody))
        {
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(ipnBody));

            httpContext.Request.Body = memoryStream;
            httpContext.Request.ContentLength = memoryStream.Length;
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    private static void HasStatusCode(IActionResult result, int statusCode)
    {
        var statusCodeActionResult = (IStatusCodeActionResult)result;

        statusCodeActionResult.StatusCode.Should().Be(statusCode);
    }

    private static void Logged(ICacheLogger logger, LogLevel logLevel, string message)
    {
        logger.Last.Should().NotBeNull();
        logger.Last!.LogLevel.Should().Be(logLevel);
        logger.Last!.Message.Should().Be(message);
    }

    private static void LoggedError(ICacheLogger logger, string message)
        => Logged(logger, LogLevel.Error, message);

    private static void LoggedWarning(ICacheLogger logger, string message)
        => Logged(logger, LogLevel.Warning, message);
}
