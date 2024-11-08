#nullable enable
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.MockedHttpClient;
using NSubstitute;

namespace Bit.Core.Test.AutoFixture;

public class HttpClientFactoryBuilder : ISpecimenBuilder
{
    private MockedHttpMessageHandler? _mockedHttpMessageHandler;

    public object Create(object request, ISpecimenContext context)
    {
        var type = request as Type;
        if (type == typeof(IHttpClientFactory))
        {
            var handler = context.Create<MockedHttpMessageHandler>();
            var httpClientFactory = Substitute.For<IHttpClientFactory>();
            httpClientFactory.CreateClient(Arg.Any<string>()).Returns(_ => handler.ToHttpClient());
            return httpClientFactory;
        }

        if (type == typeof(MockedHttpMessageHandler))
        {
            return _mockedHttpMessageHandler ??= new MockedHttpMessageHandler();
        }

        if (type == typeof(HttpClient))
        {
            var handler = context.Create<MockedHttpMessageHandler>();
            return handler.ToHttpClient();
        }

        return new NoSpecimen();
    }
}

public class HttpClientCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new HttpClientFixtures();
}

public class HttpClientFixtures : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new HttpClientFactoryBuilder());
    }
}
