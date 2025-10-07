using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;

namespace Bit.SeederApi.Controllers;

public class SeedRequestModel
{
    [Required]
    public required string Template { get; set; }
    public JsonElement? Arguments { get; set; }
}

[Route("")]
public class SeedController : Controller
{
    private readonly ILogger<SeedController> _logger;
    private readonly DatabaseContext _databaseContext;

    public SeedController(ILogger<SeedController> logger, DatabaseContext databaseContext)
    {
        _logger = logger;
        _databaseContext = databaseContext;
    }

    [HttpPost("/seed")]
    public IActionResult Seed([FromBody] SeedRequestModel request)
    {
        _logger.LogInformation("Seeding with template: {Template}", request.Template);

        try
        {
            // Find the recipe class
            var recipeTypeName = $"Bit.Seeder.Recipes.{request.Template}";
            var recipeType = Assembly.Load("Seeder")
                .GetTypes()
                .FirstOrDefault(t => t.FullName == recipeTypeName);

            if (recipeType == null)
            {
                return NotFound(new { Error = $"Recipe '{request.Template}' not found" });
            }

            // Instantiate the recipe with DatabaseContext
            var recipeInstance = Activator.CreateInstance(recipeType, _databaseContext);
            if (recipeInstance == null)
            {
                return StatusCode(500, new { Error = "Failed to instantiate recipe" });
            }

            // Find the Seed method
            var seedMethod = recipeType.GetMethod("Seed");
            if (seedMethod == null)
            {
                return StatusCode(500, new { Error = $"Seed method not found in recipe '{request.Template}'" });
            }

            // Parse arguments and match to method parameters
            var parameters = seedMethod.GetParameters();
            var arguments = new object?[parameters.Length];

            if (request.Arguments == null && parameters.Length > 0)
            {
                return BadRequest(new { Error = "Arguments are required for this recipe" });
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var parameterName = parameter.Name!;

                if (request.Arguments?.TryGetProperty(parameterName, out JsonElement value) == true)
                {
                    try
                    {
                        arguments[i] = JsonSerializer.Deserialize(value.GetRawText(), parameter.ParameterType);
                    }
                    catch (JsonException ex)
                    {
                        return BadRequest(new
                        {
                            Error = $"Failed to deserialize parameter '{parameterName}'",
                            Details = ex.Message
                        });
                    }
                }
                else if (!parameter.HasDefaultValue)
                {
                    return BadRequest(new { Error = $"Missing required parameter: {parameterName}" });
                }
                else
                {
                    arguments[i] = parameter.DefaultValue;
                }
            }

            // Invoke the Seed method
            var result = seedMethod.Invoke(recipeInstance, arguments);

            return Ok(new
            {
                Message = "Seed completed successfully",
                Template = request.Template,
                Result = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding with template: {Template}", request.Template);
            return StatusCode(500, new
            {
                Error = "An error occurred while seeding",
                Details = ex.InnerException?.Message ?? ex.Message
            });
        }
    }

    [HttpGet("/delete")]
    public string Delete()
    {
        return "hello delete";
    }
}
