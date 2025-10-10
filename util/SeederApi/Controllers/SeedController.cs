using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.SeederApi.Services;
using Microsoft.AspNetCore.Mvc;

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
            var (result, seedId) = _recipeService.ExecuteRecipe(request.Template, request.Arguments);

            return Ok(new
            {
                Message = "Seed completed successfully",
                request.Template,
                Result = result,
                SeedId = seedId
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

    [HttpDelete("/seed/batch")]
    public async Task<IActionResult> DeleteBatch([FromBody] List<Guid> seedIds)
    {
        _logger.LogInformation("Deleting batch of seeded data with IDs: {SeedIds}", string.Join(", ", seedIds));

        var aggregateException = new AggregateException();

        await Task.Run(async () =>
        {
            foreach (var seedId in seedIds)
            {
                try
                {
                    await _recipeService.DestroyRecipe(seedId);
                }
                catch (Exception ex)
                {
                    aggregateException = new AggregateException(aggregateException, ex);
                    _logger.LogError(ex, "Error deleting seeded data: {SeedId}", seedId);
                }
            }
        });

        if (aggregateException.InnerExceptions.Count > 0)
        {
            return BadRequest(new
            {
                Error = "One or more errors occurred while deleting seeded data",
                Details = aggregateException.InnerExceptions.Select(e => e.Message).ToList()
            });
        }
        return Ok(new
        {
            Message = "Batch delete completed successfully"
        });
    }

    [HttpDelete("/seed/{seedId}")]
    public async Task<IActionResult> Delete([FromRoute] Guid seedId)
    {
        _logger.LogInformation("Deleting seeded data with ID: {SeedId}", seedId);

        try
        {
            var result = await _recipeService.DestroyRecipe(seedId);

            return Ok(new
            {
                Message = "Delete completed successfully",
                Result = result
            });
        }
        catch (RecipeExecutionException ex)
        {
            _logger.LogError(ex, "Error deleting seeded data: {SeedId}", seedId);
            return BadRequest(new
            {
                Error = ex.Message,
                Details = ex.InnerException?.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting seeded data: {SeedId}", seedId);
            return StatusCode(500, new
            {
                Error = "An unexpected error occurred while deleting",
                Details = ex.Message
            });
        }
    }


    [HttpDelete("/seed")]
    public async Task<IActionResult> DeleteAll()
    {
        _logger.LogInformation("Deleting all seeded data");

        // Pull all Seeded Data ids
        var seededData = _recipeService.GetAllSeededData();

        var aggregateException = new AggregateException();

        await Task.Run(async () =>
        {
            foreach (var sd in seededData)
            {
                try
                {
                    await _recipeService.DestroyRecipe(sd.Id);
                }
                catch (Exception ex)
                {
                    aggregateException = new AggregateException(aggregateException, ex);
                    _logger.LogError(ex, "Error deleting seeded data: {SeedId}", sd.Id);
                }
            }
        });

        if (aggregateException.InnerExceptions.Count > 0)
        {
            return BadRequest(new
            {
                Error = "One or more errors occurred while deleting seeded data",
                Details = aggregateException.InnerExceptions.Select(e => e.Message).ToList()
            });
        }
        return Ok(new
        {
            Message = "All seeded data deleted successfully"
        });
    }
}
