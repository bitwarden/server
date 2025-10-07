namespace Bit.SeederApi.Services;

public class RecipeNotFoundException : Exception
{
    public RecipeNotFoundException(string message) : base(message) { }
}

public class RecipeExecutionException : Exception
{
    public RecipeExecutionException(string message) : base(message) { }
    public RecipeExecutionException(string message, Exception innerException)
        : base(message, innerException) { }
}
