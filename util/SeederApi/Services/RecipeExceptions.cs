namespace Bit.SeederApi.Services;

public class RecipeNotFoundException(string recipe) : Exception($"Recipe '{recipe}' not found");

public class RecipeExecutionException : Exception
{
    public RecipeExecutionException(string message) : base(message) { }
    public RecipeExecutionException(string message, Exception innerException)
        : base(message, innerException) { }
}
