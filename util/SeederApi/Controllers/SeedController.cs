using Bit.SeederApi.Models.Requests;
using Bit.SeederApi.Models.Response;
using Bit.SeederApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.SeederApi.Controllers;

[Route("seed")]
public class SeedController(ILogger<SeedController> logger, ISceneService sceneService, IServiceProvider serviceProvider)
    : Controller
{
    [HttpPost]
    public async Task<IActionResult> Seed([FromBody] SeedRequestModel request)
    {
        logger.LogInformation("Received seed request {Provider}", serviceProvider.GetType().FullName);
        logger.LogInformation("Seeding with template: {Template}", request.Template);

        try
        {
            SceneResponseModel response = await sceneService.ExecuteScene(request.Template, request.Arguments);

            return Json(response);
        }
        catch (SceneNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (SceneExecutionException ex)
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
    public async Task<IActionResult> DeleteBatch([FromBody] List<string> playIds)
    {
        logger.LogInformation("Deleting batch of seeded data with IDs: {PlayIds}", string.Join(", ", playIds));

        var aggregateException = new AggregateException();

        await Task.Run(async () =>
        {
            foreach (var playId in playIds)
            {
                try
                {
                    await sceneService.DestroyScene(playId);
                }
                catch (Exception ex)
                {
                    aggregateException = new AggregateException(aggregateException, ex);
                    logger.LogError(ex, "Error deleting seeded data: {SeedId}", playId);
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

    [HttpDelete("{playId}")]
    public async Task<IActionResult> Delete([FromRoute] string playId)
    {
        logger.LogInformation("Deleting seeded data with ID: {PlayId}", playId);

        try
        {
            var result = await sceneService.DestroyScene(playId);

            return Json(result);
        }
        catch (SceneExecutionException ex)
        {
            logger.LogError(ex, "Error deleting seeded data: {PlayId}", playId);
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

        var playIds = sceneService.GetAllPlayIds();

        var aggregateException = new AggregateException();

        foreach (var playId in playIds)
        {
            try
            {
                await sceneService.DestroyScene(playId);
            }
            catch (Exception ex)
            {
                aggregateException = new AggregateException(aggregateException, ex);
                logger.LogError(ex, "Error deleting seeded data: {PlayId}", playId);
            }
        }

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
