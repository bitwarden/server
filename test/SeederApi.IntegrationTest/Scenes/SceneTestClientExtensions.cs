using System.Text.Json;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.IntegrationTestCommon.Factories;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Models.Response;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Scenes;

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

    public static async Task<T> QueryAsync<T>(
        this WebApplicationFactoryBase<Startup> factory, Func<DatabaseContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<DatabaseContext>());
    }
}
