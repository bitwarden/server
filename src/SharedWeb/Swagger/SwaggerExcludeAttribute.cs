namespace Bit.SharedWeb.Swagger;

/// <summary>
/// Attribute to mark controller functions that should have some of their routes
///  hidden from Swagger documentation. This is useful for actions that support
/// both a `DELETE /` and a `POST /delete` methods, so that the schema does not
/// have duplicate operations.
/// </summary>
/// <param name="httpMethod">The HTTP method for this action to hide from the schema.
/// It should match one of the [Http*] annotations</param>
/// <param name="path">A part of the path to match, if the action has multiple routes.
/// The path provided here has to be contained in the action's route to match. If not specified,
/// the action will be hidden for all routes with the given HTTP method.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class SwaggerExcludeAttribute(string httpMethod, string? path = null) : Attribute
{
    public string HttpMethod { get; } = httpMethod.ToUpper();
    public string? Path { get; } = path;

    public bool Matches(string httpMethod, string? path)
    {
        if (!httpMethod.Equals(HttpMethod, StringComparison.OrdinalIgnoreCase)) return false;

        if (Path == null) return true;

        return path?.Contains(Path, StringComparison.OrdinalIgnoreCase) ?? false;
    }
}
