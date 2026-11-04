using System.Text.Json;
using Bit.Core.Billing.Enums;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Bit.Seeder.Scenes;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Models.Response;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

internal record SeededUser(Guid UserId, string UserKeyB64);

internal record SeededOrganization(
    Guid OrganizationId, Guid OrganizationUserId, string OrganizationKeyB64, SeededUser Owner);

internal static class SceneTestClientExtensions
{
    public static Task<HttpResponseMessage> PostSceneRequestAsync(
        this HttpClient client, string playId, string template, object arguments) =>
        client.PostAsJsonAsync("/seed", new SeedRequestModel
        {
            Template = template,
            Arguments = JsonSerializer.SerializeToElement(arguments, arguments.GetType())
        }, playId);

    public static async Task<JsonElement> PostSceneAsync(
        this HttpClient client, string playId, string template, object arguments)
    {
        var response = await client.PostSceneRequestAsync(playId, template, arguments);
        response.EnsureSuccessStatusCode();

        var model = await response.Content.ReadFromJsonAsync<SceneResponseModel>();
        Assert.NotNull(model);
        Assert.NotNull(model!.Result);
        return (JsonElement)model.Result!;
    }

    public static async Task<SeededUser> SeedUserAsync(this HttpClient client, string playId)
    {
        var result = await client.PostSceneAsync(playId, nameof(SingleUserScene), new SingleUserScene.Request
        {
            Email = $"user-{Guid.NewGuid():N}@example.com",
            Password = "asdfasdfasdf",
            EmailVerified = true
        });

        return new SeededUser(result.GetProperty("userId").GetGuid(), result.GetProperty("decryptedKeyB64").GetString()!);
    }

    public static async Task<SeededOrganization> SeedOrganizationAsync(this HttpClient client, string playId)
    {
        var owner = await client.SeedUserAsync(playId);
        var result = await client.PostSceneAsync(playId, nameof(SingleOrganizationScene), new SingleOrganizationScene.Request
        {
            OwnerUserId = owner.UserId,
            PlanType = PlanType.EnterpriseAnnually,
            Name = "Guard Org",
            Domain = $"guard-{Guid.NewGuid():N}.example.com",
            Seats = 10
        });

        return new SeededOrganization(
            result.GetProperty("organizationId").GetGuid(),
            result.GetProperty("organizationUserId").GetGuid(),
            result.GetProperty("organizationKeyB64").GetString()!,
            owner);
    }

    public static async Task<Guid> SeedFolderAsync(this HttpClient client, string playId, SeededUser user)
    {
        var result = await client.PostSceneAsync(playId, nameof(UserFolderScene), new UserFolderScene.Request
        {
            UserId = user.UserId,
            UserKeyB64 = user.UserKeyB64,
            FolderName = "Folder"
        });

        return result.GetProperty("folderId").GetGuid();
    }

    public static async Task<T> QueryAsync<T>(
        this WebApplicationFactoryBase<Startup> factory, Func<DatabaseContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<DatabaseContext>());
    }
}
