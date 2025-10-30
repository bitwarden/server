using Bit.SeederApi.Models.Requests;
using Bit.SeederApi.Models.Response;
using Bit.SeederApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.SeederApi.Controllers;

[Route("seed")]
public class SeedController(ILogger<SeedController> logger, ISeedService recipeService)
    : Controller
{
    [HttpPost]
    public IActionResult Seed([FromBody] SeedRequestModel request)
    {
        logger.LogInformation("Seeding with template: {Template}", request.Template);

        try
        {
            SceneResponseModel response = recipeService.ExecuteScene(request.Template, request.Arguments);

            return Json(response);
        }
        catch (RecipeNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (RecipeExecutionException ex)
        {
            logger.LogError(ex, "Error executing scene: {Template}", request.Template);
            return BadRequest(new
            {
                Error = ex.Message,
                Details = ex.InnerException?.Message
            });
        }
    }

    [HttpDelete("batch")]
    public async Task<IActionResult> DeleteBatch([FromBody] List<Guid> seedIds)
    {
        logger.LogInformation("Deleting batch of seeded data with IDs: {SeedIds}", string.Join(", ", seedIds));

        var aggregateException = new AggregateException();

        await Task.Run(async () =>
        {
            foreach (var seedId in seedIds)
            {
                try
                {
                    await recipeService.DestroyRecipe(seedId);
                }
                catch (Exception ex)
                {
                    aggregateException = new AggregateException(aggregateException, ex);
                    logger.LogError(ex, "Error deleting seeded data: {SeedId}", seedId);
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

    [HttpDelete("{seedId}")]
    public async Task<IActionResult> Delete([FromRoute] Guid seedId)
    {
        logger.LogInformation("Deleting seeded data with ID: {SeedId}", seedId);

        try
        {
            var result = await recipeService.DestroyRecipe(seedId);

            return Json(result);
        }
        catch (RecipeExecutionException ex)
        {
            logger.LogError(ex, "Error deleting seeded data: {SeedId}", seedId);
            return BadRequest(new
            {
                Error = ex.Message,
                Details = ex.InnerException?.Message
            });
        }
    }


    [HttpDelete]
    public async Task<IActionResult> DeleteAll()
    {
        logger.LogInformation("Deleting all seeded data");

        // Pull all Seeded Data ids
        var seededData = recipeService.GetAllSeededData();

        var aggregateException = new AggregateException();

        await Task.Run(async () =>
        {
            foreach (var sd in seededData)
            {
                try
                {
                    await recipeService.DestroyRecipe(sd.Id);
                }
                catch (Exception ex)
                {
                    aggregateException = new AggregateException(aggregateException, ex);
                    logger.LogError(ex, "Error deleting seeded data: {SeedId}", sd.Id);
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
        return NoContent();
    }
}
