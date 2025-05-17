using Bit.Identity.IdentityServer;
using Duende.IdentityServer.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Bit.Identity.Test.IdentityServer;

public class DynamicClientStoreTests
{
    private readonly IServiceCollection _services;
    private readonly IClientProvider _apiKeyProvider;

    private readonly Func<DynamicClientStore> _sutCreator;

    public DynamicClientStoreTests()
    {
        _services = new ServiceCollection();
        _apiKeyProvider = Substitute.For<IClientProvider>();

        _sutCreator = () => new DynamicClientStore(
            _services.BuildServiceProvider(),
            _apiKeyProvider,
            new StaticClientStore(new Core.Settings.GlobalSettings())
        );
    }

    [Theory]
    [InlineData("mobile")]
    [InlineData("web")]
    [InlineData("browser")]
    [InlineData("desktop")]
    [InlineData("cli")]
    [InlineData("connector")]
    public async Task FindClientByIdAsync_StaticClients_Works(string staticClientId)
    {
        var sut = _sutCreator();

        var client = await sut.FindClientByIdAsync(staticClientId);

        Assert.NotNull(client);
        Assert.Equal(staticClientId, client.ClientId);
    }

    [Fact]
    public async Task FindClientByIdAsync_SplitName_NoService_ReturnsNull()
    {
        _services.AddClientProvider<FakeClientProvider>("my-provider");

        var sut = _sutCreator();

        var client = await sut.FindClientByIdAsync("blah.something");

        Assert.Null(client);

        await _apiKeyProvider
            .Received(0)
            .GetAsync(Arg.Any<string>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FindClientByIdAsync_SplitName_HasService_ReturnsValueFromService(bool returnNull)
    {
        var fakeProvider = Substitute.For<IClientProvider>();

        fakeProvider
            .GetAsync("something")
            .Returns(returnNull ? null : new Client { ClientId = "fake" });

        _services.AddKeyedSingleton("my-provider", fakeProvider);

        var sut = _sutCreator();

        var client = await sut.FindClientByIdAsync("my-provider.something");

        if (returnNull)
        {
            Assert.Null(client);
        }
        else
        {
            Assert.NotNull(client);
        }

        await fakeProvider
            .Received(1)
            .GetAsync("something");

        await _apiKeyProvider
            .Received(0)
            .GetAsync(Arg.Any<string>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FindClientByIdAsync_RandomString_NotSplit_TriesApiKey(bool returnsNull)
    {
        _apiKeyProvider
            .GetAsync("random-string")
            .Returns(returnsNull ? null : new Client { ClientId = "test" });

        var sut = _sutCreator();

        var client = await sut.FindClientByIdAsync("random-string");

        if (returnsNull)
        {
            Assert.Null(client);
        }
        else
        {
            Assert.NotNull(client);
        }

        await _apiKeyProvider
            .Received(1)
            .GetAsync("random-string");
    }

    [Theory]
    [InlineData("id.")]
    [InlineData("id.    ")]
    public async Task FindClientByIdAsync_InvalidIdentifierValue_ReturnsNull(string clientId)
    {
        var sut = _sutCreator();

        var client = await sut.FindClientByIdAsync(clientId);
        Assert.Null(client);
    }

    private class FakeClientProvider : IClientProvider
    {
        public FakeClientProvider()
        {
            Fake = Substitute.For<IClientProvider>();
        }

        public IClientProvider Fake { get; }

        public Task<Client?> GetAsync(string identifier)
        {
            return Fake.GetAsync(identifier);
        }
    }
}
