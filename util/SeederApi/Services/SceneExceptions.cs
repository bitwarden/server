namespace Bit.SeederApi.Services;

public class SceneNotFoundException(string scene) : Exception($"Scene '{scene}' not found");

public class SceneExecutionException : Exception
{
    public SceneExecutionException(string message) : base(message) { }
    public SceneExecutionException(string message, Exception innerException)
        : base(message, innerException) { }
}
