using System;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class StripePaymentServiceTests
    {
        private readonly StripePaymentService _sut;

        private readonly ITransactionRepository _transactionRepository;
        private readonly GlobalSettings _globalSettings;
        private readonly ILogger<StripePaymentService> _logger;

        public StripePaymentServiceTests()
        {
            _transactionRepository = Substitute.For<ITransactionRepository>();
            _globalSettings = new GlobalSettings();
            _logger = Substitute.For<ILogger<StripePaymentService>>();

            _sut = new StripePaymentService(
                _transactionRepository,
                _globalSettings,
                _logger
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
