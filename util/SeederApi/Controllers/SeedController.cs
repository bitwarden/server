using Bit.SeederApi.Commands.Interfaces;
using Bit.SeederApi.Execution;
using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Queries.Interfaces;
using Bit.SeederApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.SeederApi.Controllers;

[Route("seed")]
public class SeedController(
    ILogger<SeedController> logger,
    ISceneExecutor sceneExecutor,
    IDestroySceneCommand destroySceneCommand,
    IDestroyBatchScenesCommand destroyBatchScenesCommand,
    IGetAllPlayIdsQuery getAllPlayIdsQuery) : Controller
{
    [HttpPost]
    public async Task<IActionResult> SeedAsync([FromBody] SeedRequestModel request)
    {
        logger.LogInformation("Received seed request with template: {Template}", request.Template);

        try
        {
            var response = await sceneExecutor.ExecuteAsync(request.Template, request.Arguments);

            return Json(response);
        }
        catch (SceneNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (SceneExecutionException ex)
        {
            logger.LogError(ex, "Error executing scene: {Template}", request.Template);
            return BadRequest(new { Error = ex.Message, Details = ex.InnerException?.Message });
        }
    }

    [HttpDelete("batch")]
    public async Task<IActionResult> DeleteBatchAsync([FromBody] List<string> playIds)
    {
        logger.LogInformation("Deleting batch of seeded data with IDs: {PlayIds}", string.Join(", ", playIds));

        try
        {
            await destroyBatchScenesCommand.DestroyAsync(playIds);
            return Ok(new { Message = "Batch delete completed successfully" });
        }
        catch (AggregateException ex)
        {
            return BadRequest(new
            {
                Error = ex.Message,
                Details = ex.InnerExceptions.Select(e => e.Message).ToList()
            });
        }
    }

    [HttpDelete("{playId}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] string playId)
    {
        logger.LogInformation("Deleting seeded data with ID: {PlayId}", playId);

        try
        {
            var result = await destroySceneCommand.DestroyAsync(playId);

            return Json(result);
        }
        catch (SceneExecutionException ex)
        {
            logger.LogError(ex, "Error deleting seeded data: {PlayId}", playId);
            return BadRequest(new { Error = ex.Message, Details = ex.InnerException?.Message });
        }
    }


    [HttpDelete]
    public async Task<IActionResult> DeleteAllAsync()
    {
        logger.LogInformation("Deleting all seeded data");

        var playIds = getAllPlayIdsQuery.GetAllPlayIds();

        try
        {
            await destroyBatchScenesCommand.DestroyAsync(playIds);
            return NoContent();
        }
        catch (AggregateException ex)
        {
            return BadRequest(new
            {
                Error = ex.Message,
                Details = ex.InnerExceptions.Select(e => e.Message).ToList()
            });
        }
    }
}
