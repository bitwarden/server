using Bit.SeederApi.Models.Requests;
using Bit.SeederApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.SeederApi.Controllers
{
    [Route("query")]
    public class QueryController(ILogger<QueryController> logger, IRecipeService recipeService)
        : Controller
    {
        [HttpPost]
        public IActionResult Query([FromBody] QueryRequestModel request)
        {
            logger.LogInformation("Executing query: {Query}", request.Template);

            try
            {
                var result = recipeService.ExecuteQuery(request.Template, request.Arguments);

                return Json(new { Result = result });
            }
            catch (RecipeNotFoundException ex)
            {
                return NotFound(new { Error = ex.Message });
            }
            catch (RecipeExecutionException ex)
            {
                logger.LogError(ex, "Error executing query: {Query}", request.Template);
                return BadRequest(new
                {
                    Error = ex.Message,
                    Details = ex.InnerException?.Message
                });
            }
        }
    }
}
