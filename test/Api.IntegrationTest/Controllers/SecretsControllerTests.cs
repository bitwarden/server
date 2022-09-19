using System.Net.Http.Headers;
using System.Text.Json;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.Controllers;

public class SecretsControllerTest : IClassFixture<ApiApplicationFactory>
{
    private readonly string _mockEncryptedString =
        "2.3Uk+WNBIoU5xzmVFNcoWzz==|1MsPIYuRfdOHfu/0uY6H2Q==|/98sp4wb6pHP1VTZ9JcNCYgQjEUMFPlqJgCwRk1YXKg=";

    private readonly int _secretsToDelete = 3;
    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly ISecretRepository _secretRepository;

    public SecretsControllerTest(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _secretRepository = _factory.GetService<ISecretRepository>();
    }

    [Fact]
    public async Task DeleteSecrets()
    {
        var tokens = await _factory.LoginWithNewAccount();
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory);

        var secretIds = new List<Guid>();
        for (var i = 0; i < _secretsToDelete; i++)
        {
            var secret = await _secretRepository.CreateAsync(new Secret
            {
                OrganizationId = organization.Id,
                Key = _mockEncryptedString,
                Value = _mockEncryptedString,
                Note = _mockEncryptedString
            });
            secretIds.Add(secret.Id);
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.Token);
        var response = await _client.PostAsync("/secrets/delete", JsonContent.Create(secretIds));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);

        var jsonResult = JsonDocument.Parse(content);
        var index = 0;
        foreach (var element in jsonResult.RootElement.GetProperty("data").EnumerateArray())
        {
            Assert.Equal(secretIds[index].ToString(), element.GetProperty("id").ToString());
            Assert.Empty(element.GetProperty("error").ToString());
            index++;
        }

        var secrets = await _secretRepository.GetManyByIds(secretIds);
        Assert.Empty(secrets);
    }
}
