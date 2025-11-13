using Bit.SeederApi.Models.Request;
using Bit.SeederApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.SeederApi.Controllers;

[Route("query")]
public class QueryController(ILogger<QueryController> logger, IQueryService queryService) : Controller
{
    [HttpPost]
    public IActionResult Query([FromBody] QueryRequestModel request)
    {
        logger.LogInformation("Executing query: {Query}", request.Template);

        try
        {
            var result = queryService.ExecuteQuery(request.Template, request.Arguments);

            return Json(result);
        }
        catch (SceneNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
        catch (SceneExecutionException ex)
        {
            logger.LogError(ex, "Error executing query: {Query}", request.Template);
            return BadRequest(new { Error = ex.Message, Details = ex.InnerException?.Message });
        }
    }
}
