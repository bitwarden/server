using System;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Braintree;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class StripePaymentServiceTests
    {
        private readonly StripePaymentService _sut;

        private readonly ITransactionRepository _transactionRepository;
        private readonly IUserRepository _userRepository;
        private readonly IAppleIapService _appleIapService;
        private readonly GlobalSettings _globalSettings;
        private readonly ILogger<StripePaymentService> _logger;
        private readonly ITaxRateRepository _taxRateRepository;
        private readonly IStripeAdapter _stripeAdapter;
        private readonly IBraintreeGateway _braintreeGateway;

        public StripePaymentServiceTests()
        {
            _transactionRepository = Substitute.For<ITransactionRepository>();
            _userRepository = Substitute.For<IUserRepository>();
            _appleIapService = Substitute.For<IAppleIapService>();
            _globalSettings = new GlobalSettings();
            _logger = Substitute.For<ILogger<StripePaymentService>>();
            _taxRateRepository = Substitute.For<ITaxRateRepository>();
            _stripeAdapter = Substitute.For<IStripeAdapter>();
            _braintreeGateway = Substitute.For<IBraintreeGateway>();

            _sut = new StripePaymentService(
                _transactionRepository,
                _userRepository,
                _appleIapService,
                _logger,
                _taxRateRepository,
                _stripeAdapter,
                _braintreeGateway
            );
        }

        // Remove this test when we add actual tests. It only proves that
        // we've properly constructed the system under test.
        [Fact]
        public void ServiceExists()
        {
            Assert.NotNull(_sut);
        }
    }
}
