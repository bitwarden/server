using Bit.Core.Settings;
using Bit.Core.Vault.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class AzureAttachmentStorageServiceTests
{
    private readonly AzureAttachmentStorageService _sut;

    private readonly GlobalSettings _globalSettings;
    private readonly ILogger<AzureAttachmentStorageService> _logger;

    public AzureAttachmentStorageServiceTests()
    {
        _globalSettings = new GlobalSettings();
        _logger = Substitute.For<ILogger<AzureAttachmentStorageService>>();

        _sut = new AzureAttachmentStorageService(_globalSettings, _logger);
    }

    // Remove this test when we add actual tests. It only proves that
    // we've properly constructed the system under test.
    [Fact(Skip = "Needs additional work")]
    public void ServiceExists()
    {
        Assert.NotNull(_sut);
    }
}
