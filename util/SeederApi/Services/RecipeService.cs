using System.Reflection;
using System.Text.Json;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder;

namespace Bit.SeederApi.Services;

public class RecipeService : IRecipeService
{
    private readonly DatabaseContext _databaseContext;
    private readonly ILogger<RecipeService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public RecipeService(DatabaseContext databaseContext, ILogger<RecipeService> logger, IServiceProvider serviceProvider)
    {
        _databaseContext = databaseContext;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public List<SeededData> GetAllSeededData()
    {
        return _databaseContext.SeededData.ToList();
    }

    public (object? Result, Guid? SeedId) ExecuteRecipe(string templateName, JsonElement? arguments)
    {
        var result = ExecuteRecipeMethod(templateName, arguments, "Seed");

        if (result is not RecipeResult recipeResult)
        {
            return (Result: result, SeedId: null);
        }

        if (recipeResult.TrackedEntities.Count == 0)
        {
            return (Result: recipeResult.Result, SeedId: null);
        }

        var seededData = new SeededData
        {
            Id = Guid.NewGuid(),
            RecipeName = templateName,
            Data = JsonSerializer.Serialize(recipeResult.TrackedEntities),
            CreationDate = DateTime.UtcNow
        };

        _databaseContext.Add(seededData);
        _databaseContext.SaveChanges();

        _logger.LogInformation("Saved seeded data with ID {SeedId} for recipe {RecipeName}",
            seededData.Id, templateName);

        return (Result: recipeResult.Result, SeedId: seededData.Id);
    }

    public object? DestroyRecipe(Guid seedId)
    {
        var seededData = _databaseContext.SeededData.FirstOrDefault(s => s.Id == seedId);
        if (seededData == null)
        {
            throw new RecipeExecutionException($"Seeded data with ID {seedId} not found");
        }

        var trackedEntities = JsonSerializer.Deserialize<Dictionary<string, List<Guid>>>(seededData.Data);
        if (trackedEntities == null)
        {
            throw new RecipeExecutionException($"Failed to deserialize tracked entities for seed ID {seedId}");
        }

        // Delete in reverse order to respect foreign key constraints
        if (trackedEntities.TryGetValue("User", out var userIds))
        {
            var users = _databaseContext.Users.Where(u => userIds.Contains(u.Id));
            _databaseContext.RemoveRange(users);
        }

        if (trackedEntities.TryGetValue("Organization", out var orgIds))
        {
            var organizations = _databaseContext.Organizations.Where(o => orgIds.Contains(o.Id));
            _databaseContext.RemoveRange(organizations);
        }

        _databaseContext.Remove(seededData);
        _databaseContext.SaveChanges();

        _logger.LogInformation("Successfully destroyed seeded data with ID {SeedId} for recipe {RecipeName}",
            seedId, seededData.RecipeName);

        return new { SeedId = seedId, RecipeName = seededData.RecipeName };
    }

    private object? ExecuteRecipeMethod(string templateName, JsonElement? arguments, string methodName)
    {
        try
        {
            var recipeType = LoadRecipeType(templateName);
            var method = GetRecipeMethod(recipeType, templateName, methodName);
            var recipeInstance = CreateRecipeInstance(recipeType);

            var methodArguments = ParseMethodArguments(method, arguments);
            var result = method.Invoke(recipeInstance, methodArguments);

            _logger.LogInformation("Successfully executed {MethodName} on recipe: {TemplateName}", methodName, templateName);
            return result;
        }
        catch (Exception ex) when (ex is not RecipeNotFoundException and not RecipeExecutionException)
        {
            _logger.LogError(ex, "Unexpected error executing {MethodName} on recipe: {TemplateName}", methodName, templateName);
            throw new RecipeExecutionException(
                $"An unexpected error occurred while executing {methodName} on recipe '{templateName}'",
                ex.InnerException ?? ex);
        }
    }

    private static Type LoadRecipeType(string templateName)
    {
        var recipeTypeName = $"Bit.Seeder.Recipes.{templateName}";
        var recipeType = Assembly.Load("Seeder")
            .GetTypes()
            .FirstOrDefault(t => t.FullName == recipeTypeName);

        return recipeType ?? throw new RecipeNotFoundException(templateName);
    }

    private static MethodInfo GetRecipeMethod(Type recipeType, string templateName, string methodName)
    {
        var method = recipeType.GetMethod(methodName);
        return method ?? throw new RecipeExecutionException($"{methodName} method not found in recipe '{templateName}'");
    }

    private object CreateRecipeInstance(Type recipeType)
    {
        var constructors = recipeType.GetConstructors();
        if (constructors.Length == 0)
        {
            throw new RecipeExecutionException($"No public constructors found for recipe type '{recipeType.Name}'");
        }

        var constructor = constructors[0];
        var parameters = constructor.GetParameters();
        var constructorArgs = new object[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var service = _serviceProvider.GetService(parameter.ParameterType);

            if (service == null)
            {
                throw new RecipeExecutionException(
                    $"Unable to resolve service of type '{parameter.ParameterType.Name}' for recipe constructor");
            }

            constructorArgs[i] = service;
        }

        return Activator.CreateInstance(recipeType, constructorArgs)!;
    }

    private static object?[] ParseMethodArguments(MethodInfo seedMethod, JsonElement? arguments)
    {
        var parameters = seedMethod.GetParameters();

        if (arguments == null && parameters.Length > 0)
        {
            throw new RecipeExecutionException("Arguments are required for this recipe");
        }

        var methodArguments = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var parameterName = parameter.Name!;

            if (arguments?.TryGetProperty(parameterName, out var value) == true)
            {
                try
                {
                    methodArguments[i] = JsonSerializer.Deserialize(value.GetRawText(), parameter.ParameterType);
                }
                catch (JsonException ex)
                {
                    throw new RecipeExecutionException(
                        $"Failed to deserialize parameter '{parameterName}': {ex.Message}", ex);
                }
            }
            else if (!parameter.HasDefaultValue)
            {
                throw new RecipeExecutionException($"Missing required parameter: {parameterName}");
            }
            else
            {
                methodArguments[i] = parameter.DefaultValue;
            }
        }

        return methodArguments;
    }
}
