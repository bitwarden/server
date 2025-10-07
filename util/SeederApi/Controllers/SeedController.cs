using Bit.SeederApi.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
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
    private readonly IRecipeService _recipeService;

    public SeedController(ILogger<SeedController> logger, IRecipeService recipeService)
    {
        _logger = logger;
        _recipeService = recipeService;
    }

    [HttpPost("/seed")]
    public IActionResult Seed([FromBody] SeedRequestModel request)
    {
        _logger.LogInformation("Seeding with template: {Template}", request.Template);

        try
        {
            var result = _recipeService.ExecuteRecipe(request.Template, request.Arguments);

            return Ok(new
            {
                Message = "Seed completed successfully",
                request.Template,
                Result = result
            });
        }
        catch (RecipeNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (RecipeExecutionException ex)
        {
            _logger.LogError(ex, "Error executing recipe: {Template}", request.Template);
            return BadRequest(new
            {
                Error = ex.Message,
                Details = ex.InnerException?.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error seeding with template: {Template}", request.Template);
            return StatusCode(500, new
            {
                Error = "An unexpected error occurred while seeding",
                Details = ex.Message
            });
        }
    }

    [HttpDelete("/delete")]
    public IActionResult Delete([FromBody] SeedRequestModel request)
    {
        _logger.LogInformation("Deleting with template: {Template}", request.Template);

        try
        {
            var result = _recipeService.DestroyRecipe(request.Template, request.Arguments);

            return Ok(new
            {
                Message = "Delete completed successfully",
                request.Template,
                Result = result
            });
        }
        catch (RecipeNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (RecipeExecutionException ex)
        {
            _logger.LogError(ex, "Error executing recipe delete: {Template}", request.Template);
            return BadRequest(new
            {
                Error = ex.Message,
                Details = ex.InnerException?.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting with template: {Template}", request.Template);
            return StatusCode(500, new
            {
                Error = "An unexpected error occurred while deleting",
                Details = ex.Message
            });
        }
    }
}
