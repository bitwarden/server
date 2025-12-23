using System.Net;
using System.Net.Http.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Repositories;
using Bit.Events.Models;

namespace Bit.Events.IntegrationTest.Controllers;

public class CollectControllerTests : IAsyncLifetime
{
    private EventsApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _ownerEmail = null!;
    private Guid _ownerId;

    public async ValueTask InitializeAsync()
    {
        _factory = new EventsApplicationFactory();
        _ownerEmail = $"integration-test+{Guid.NewGuid()}@bitwarden.com";
        var (accessToken, _) = await _factory.LoginWithNewAccount(_ownerEmail);
        _client = _factory.CreateAuthedClient(accessToken);

        // Get the user ID
        var userRepository = _factory.GetService<IUserRepository>();
        var user = await userRepository.GetByEmailAsync(_ownerEmail);
        _ownerId = user!.Id;
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Post_NullModel_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>?>("collect", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_EmptyModel_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("collect", Array.Empty<EventModel>());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_UserClientExportedVault_Success()
    {
        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.User_ClientExportedVault,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_CipherClientAutofilled_WithValidCipher_Success()
    {
        var cipher = await CreateCipherForUserAsync(_ownerId);

        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_CipherClientCopiedPassword_WithValidCipher_Success()
    {
        var cipher = await CreateCipherForUserAsync(_ownerId);

        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.Cipher_ClientCopiedPassword,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_CipherClientCopiedHiddenField_WithValidCipher_Success()
    {
        var cipher = await CreateCipherForUserAsync(_ownerId);

        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.Cipher_ClientCopiedHiddenField,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_CipherClientCopiedCardCode_WithValidCipher_Success()
    {
        var cipher = await CreateCipherForUserAsync(_ownerId);

        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.Cipher_ClientCopiedCardCode,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_CipherClientToggledCardNumberVisible_WithValidCipher_Success()
    {
        var cipher = await CreateCipherForUserAsync(_ownerId);

        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.Cipher_ClientToggledCardNumberVisible,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_CipherClientToggledCardCodeVisible_WithValidCipher_Success()
    {
        var cipher = await CreateCipherForUserAsync(_ownerId);

        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.Cipher_ClientToggledCardCodeVisible,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_CipherClientToggledHiddenFieldVisible_WithValidCipher_Success()
    {
        var cipher = await CreateCipherForUserAsync(_ownerId);

        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.Cipher_ClientToggledHiddenFieldVisible,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_CipherClientToggledPasswordVisible_WithValidCipher_Success()
    {
        var cipher = await CreateCipherForUserAsync(_ownerId);

        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.Cipher_ClientToggledPasswordVisible,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_CipherClientViewed_WithValidCipher_Success()
    {
        var cipher = await CreateCipherForUserAsync(_ownerId);

        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.Cipher_ClientViewed,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_CipherEvent_WithoutCipherId_Success()
    {
        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_CipherEvent_WithInvalidCipherId_Success()
    {
        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = Guid.NewGuid(),
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_OrganizationClientExportedVault_WithValidOrganization_Success()
    {
        var organization = await CreateOrganizationAsync(_ownerId);

        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.Organization_ClientExportedVault,
                OrganizationId = organization.Id,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_OrganizationClientExportedVault_WithoutOrganizationId_Success()
    {
        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.Organization_ClientExportedVault,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_OrganizationClientExportedVault_WithInvalidOrganizationId_Success()
    {
        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.Organization_ClientExportedVault,
                OrganizationId = Guid.NewGuid(),
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_MultipleEvents_Success()
    {
        var cipher = await CreateCipherForUserAsync(_ownerId);
        var organization = await CreateOrganizationAsync(_ownerId);

        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.User_ClientExportedVault,
                Date = DateTime.UtcNow,
            },
            new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow,
            },
            new EventModel
            {
                Type = EventType.Cipher_ClientViewed,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow,
            },
            new EventModel
            {
                Type = EventType.Organization_ClientExportedVault,
                OrganizationId = organization.Id,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_CipherEventsBatch_MoreThan50Items_Success()
    {
        var cipher = await CreateCipherForUserAsync(_ownerId);

        // Create 60 cipher events to test batching logic (should be processed in 2 batches of 50)
        var events = Enumerable.Range(0, 60)
            .Select(_ => new EventModel
            {
                Type = EventType.Cipher_ClientViewed,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow,
            })
            .ToList();

        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect", events);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_UnsupportedEventType_Success()
    {
        // Testing with an event type not explicitly handled in the switch statement
        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.User_LoggedIn,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_MixedValidAndInvalidEvents_Success()
    {
        var cipher = await CreateCipherForUserAsync(_ownerId);

        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.User_ClientExportedVault,
                Date = DateTime.UtcNow,
            },
            new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = Guid.NewGuid(), // Invalid cipher ID
                Date = DateTime.UtcNow,
            },
            new EventModel
            {
                Type = EventType.Cipher_ClientViewed,
                CipherId = cipher.Id, // Valid cipher ID
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Post_CipherCaching_MultipleEventsForSameCipher_Success()
    {
        var cipher = await CreateCipherForUserAsync(_ownerId);

        // Multiple events for the same cipher should use caching
        var response = await _client.PostAsJsonAsync<IEnumerable<EventModel>>("collect",
        [
            new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow,
            },
            new EventModel
            {
                Type = EventType.Cipher_ClientViewed,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow,
            },
            new EventModel
            {
                Type = EventType.Cipher_ClientCopiedPassword,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow,
            },
        ]);

        response.EnsureSuccessStatusCode();
    }

    private async Task<Cipher> CreateCipherForUserAsync(Guid userId)
    {
        var cipherRepository = _factory.GetService<ICipherRepository>();

        var cipher = new Cipher
        {
            Type = CipherType.Login,
            UserId = userId,
            Data = "{\"name\":\"Test Cipher\"}",
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        };

        await cipherRepository.CreateAsync(cipher);
        return cipher;
    }

    private async Task<Organization> CreateOrganizationAsync(Guid ownerId)
    {
        var organizationRepository = _factory.GetService<IOrganizationRepository>();
        var organizationUserRepository = _factory.GetService<IOrganizationUserRepository>();

        var organization = new Organization
        {
            Name = "Test Organization",
            BillingEmail = _ownerEmail,
            Plan = "Free",
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        };

        await organizationRepository.CreateAsync(organization);

        // Add the user as an owner of the organization
        var organizationUser = new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = ownerId,
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.Owner,
        };

        await organizationUserRepository.CreateAsync(organizationUser);

        return organization;
    }
}
