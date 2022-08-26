using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Utilities
{
    public class InstallationAuthenticatingHandlerTests
    {
        private readonly IOptionsMonitor<ConnectTokenOptions> _optionsMonitor;

        public InstallationAuthenticatingHandlerTests()
        {
            _optionsMonitor = Substitute.For<IOptionsMonitor<ConnectTokenOptions>>();
        }

        [Fact]
        public async Task SendAsync_Success()
        {
            _optionsMonitor.Get("Test")
                .Returns(new ConnectTokenOptions
                {
                    ClientId = "client_id_test",
                    ClientSecret = "client_secret_test",
                    Scope = "test_scope",
                });

            var sut = CreateSut(ValidIdentityRequest);

            sut.InnerHandler = ManualHandler.AssertAuthorization(ValidAuthToken);

            var client = new HttpClient(sut)
            {
                BaseAddress = new Uri("https://test.com"),
            };

            var response = await client.GetAsync("/test");
            Assert.True(response.IsSuccessStatusCode);
        }

        [Fact]
        public async Task SendAsync_OptOut_True_Works()
        {
            _optionsMonitor.Get("Test")
                .Returns(new ConnectTokenOptions
                {
                    ClientId = "client_id_test",
                    ClientSecret = "client_secret_test",
                    Scope = "test_scope",
                });

            var sut = CreateSut(ValidIdentityRequest);
            sut.InnerHandler = new ManualHandler(request =>
            {
                // Should not have added an authorization header to the request
                Assert.Null(request.Headers.Authorization);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });

            var client = new HttpClient(sut)
            {
                BaseAddress = new Uri("https://test.com"),
            };

            var request = new HttpRequestMessage(HttpMethod.Get, "test");
            request.Options.Set(InstallationAuthenticatingHandler.OptOutKey, true);
            var response = await client.SendAsync(request);
            Assert.True(response.IsSuccessStatusCode);
        }

        [Fact]
        public async Task SendAsync_OptOut_False_Works()
        {
            _optionsMonitor.Get("Test")
                .Returns(new ConnectTokenOptions
                {
                    ClientId = "client_id_test",
                    ClientSecret = "client_secret_test",
                    Scope = "test_scope",
                });

            var sut = CreateSut(ValidIdentityRequest);
            sut.InnerHandler = ManualHandler.AssertAuthorization(auth =>
            {
                Assert.Equal("Bearer", auth.Scheme);
                Assert.Equal("test_access_token", auth.Parameter);
            });

            var client = new HttpClient(sut)
            {
                BaseAddress = new Uri("https://test.com"),
            };

            var request = new HttpRequestMessage(HttpMethod.Get, "test");
            request.Options.Set(InstallationAuthenticatingHandler.OptOutKey, false);
            var response = await client.SendAsync(request);
            Assert.True(response.IsSuccessStatusCode);
        }

        [Fact]
        public async Task SendAsync_IdentityResponseMissingAccessToken_Throws()
        {
            _optionsMonitor.Get("Test")
                .Returns(new ConnectTokenOptions
                {
                    ClientId = "client_id_test",
                    ClientSecret = "client_secret_test",
                    Scope = "test_scope",
                });

            var sut = CreateSut(request =>
            {
                return Task.FromResult(new HttpResponseMessage
                {
                    Content = JsonContent.Create(new
                    {
                        bad = "stuff",
                    }),
                });
            });

            var client = new HttpClient(sut)
            {
                BaseAddress = new Uri("https://test.com"),
            };

            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("test"));
        }

        [Fact]
        public async Task SendAsync_IdentityResponseMissingExpiresIn_Throws()
        {
            _optionsMonitor.Get("Test")
                .Returns(new ConnectTokenOptions
                {
                    ClientId = "client_id_test",
                    ClientSecret = "client_secret_test",
                    Scope = "test_scope",
                });

            var sut = CreateSut(request =>
            {
                return Task.FromResult(new HttpResponseMessage
                {
                    Content = JsonContent.Create(new
                    {
                        access_token = "stuff",
                    }),
                });
            });

            var client = new HttpClient(sut)
            {
                BaseAddress = new Uri("https://test.com"),
            };

            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("test"));
        }

        [Fact]
        public async Task SendAsync_CachesToken()
        {
            _optionsMonitor.Get("Test")
                .Returns(new ConnectTokenOptions
                {
                    ClientId = "client_id_test",
                    ClientSecret = "client_secret_test",
                    Scope = "test_scope",
                });

            var timesCalled = 0;

            var sut = CreateSut(request =>
            {
                timesCalled++;
                Assert.Equal(1, timesCalled);
                return ValidIdentityRequest(request);
            });

            sut.InnerHandler = ManualHandler.AssertAuthorization(auth =>
            {
                Assert.Equal("Bearer", auth.Scheme);
                Assert.Equal("test_access_token", auth.Parameter);
            });

            var client = new HttpClient(sut)
            {
                BaseAddress = new Uri("https://test.com"),
            };

            var response = await client.GetAsync("test");
            Assert.True(response.IsSuccessStatusCode);
            response = await client.GetAsync("test");
            Assert.True(response.IsSuccessStatusCode);
        }

        [Fact]
        public async Task SendAsync_InvalidatesCacheByTime_Success()
        {
            _optionsMonitor.Get("Test")
                .Returns(new ConnectTokenOptions
                {
                    ClientId = "client_id_test",
                    ClientSecret = "client_secret_test",
                    Scope = "test_scope",
                });

            var timesCalled = 0;

            var sut = CreateSut(request =>
            {
                timesCalled++;
                return Task.FromResult(new HttpResponseMessage
                {
                    Content = JsonContent.Create(new
                    {
                        access_token = "test_access_token",
                        expires_in = TimeSpan.FromMinutes(9).TotalSeconds,
                    }),
                });
            });

            sut.InnerHandler = ManualHandler.AssertAuthorization(auth =>
            {
                Assert.Equal("Bearer", auth.Scheme);
                Assert.Equal("test_access_token", auth.Parameter);
            });

            var client = new HttpClient(sut)
            {
                BaseAddress = new Uri("https://test.com"),
            };

            var response = await client.GetAsync("test");
            Assert.True(response.IsSuccessStatusCode);
            response = await client.GetAsync("test");
            Assert.True(response.IsSuccessStatusCode);

            Assert.Equal(2, timesCalled);
        }

        private static async Task<HttpResponseMessage> ValidIdentityRequest(HttpRequestMessage request)
        {
            Assert.NotNull(request.Content);
            Assert.Equal("application/x-www-form-urlencoded",
                request.Content.Headers.ContentType.MediaType);
            var formContent = Assert.IsAssignableFrom<FormUrlEncodedContent>(request.Content);
            Assert.Equal(
                "grant_type=client_credentials&client_id=client_id_test&client_secret=client_secret_test&scope=test_scope",
                await formContent.ReadAsStringAsync());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    access_token = "test_access_token",
                    expires_in = 3600,
                }),
            };
        }

        private static void ValidAuthToken(AuthenticationHeaderValue authenticationHeaderValue)
        {
            Assert.Equal("Bearer", authenticationHeaderValue.Scheme);
            Assert.Equal("test_access_token", authenticationHeaderValue.Parameter);
        }

        private InstallationAuthenticatingHandler CreateSut(Func<HttpRequestMessage, Task<HttpResponseMessage>> identityHandler)
        {
            return new InstallationAuthenticatingHandler(
                new HttpClient(new ManualHandler(identityHandler))
                {
                    BaseAddress = new Uri("https://identity.test.com"),
                },
                NullLogger<InstallationAuthenticatingHandler>.Instance,
                _optionsMonitor,
                "Test"
            );
        }
    }

    public class ManualHandler : HttpMessageHandler
    {
        public static ManualHandler AssertAuthorization(Action<AuthenticationHeaderValue> assertAuthorizationHeader)
        {
            return new ManualHandler(request =>
            {
                var authorizationHeader = request.Headers.Authorization;
                assertAuthorizationHeader(authorizationHeader);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            });
        }

        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _localHandler;

        public ManualHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> identityHandler)
        {
            _localHandler = identityHandler;
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _localHandler(request);
    }
}
