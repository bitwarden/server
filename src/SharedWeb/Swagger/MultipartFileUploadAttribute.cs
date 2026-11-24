namespace Bit.SharedWeb.Swagger;

/// <summary>
/// Documents the multipart/form-data body of an action that reads the request stream itself instead of
/// binding it, so the body still appears in the OpenAPI spec. See <see cref="MultipartFileUploadOperationFilter"/>.
/// </summary>
/// <param name="textFields">
/// Optional text parts sent before the file part. The server reads parts in order, so the order here is the order
/// clients must send them in.
/// </param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MultipartFileUploadAttribute(params string[] textFields) : Attribute
{
    public string[] TextFields { get; } = textFields;

    /// <summary>Name of the file part, which is always sent last.</summary>
    public string FileField { get; init; } = "data";
}
