namespace Bit.Core.Auth.Identity;

public static class Policies
{
    /// <summary>
    /// Policy for managing access to the Send feature.
    /// </summary>
    /// <remarks>
    /// <example>
    /// Can be used with the <c>Authorize</c> attribute, for example:
    /// <code>
    /// [Authorize(Policy = Policies.Send)]
    /// </code>
    /// </example>
    /// </remarks>
    public const string Send = "Send";

    /// <summary>
    /// Policy to manage access to general API endpoints.
    /// </summary>
    /// <remarks>
    /// <example>
    /// Can be used with the <c>Authorize</c> attribute, for example:
    /// <code>
    /// [Authorize(Policy = Policies.Application)]
    /// </code>
    /// </example>
    /// </remarks>
    public const string Application = "Application";

    /// <summary>
    /// Policy to manage access to API endpoints intended for use by the Web Vault and browser extension only.
    /// </summary>
    /// <remarks>
    /// <example>
    /// Can be used with the <c>Authorize</c> attribute, for example:
    /// <code>
    /// [Authorize(Policy = Policies.Web)]
    /// </code>
    /// </example>
    /// </remarks>
    public const string Web = "Web";

    /// <summary>
    /// Policy to restrict access to API endpoints for the Push feature.
    /// </summary>
    /// <remarks>
    /// <example>
    /// Can be used with the <c>Authorize</c> attribute, for example:
    /// <code>
    /// [Authorize(Policy = Policies.Push)]
    /// </code>
    /// </example>
    /// </remarks>
    public const string Push = "Push";

    // TODO: This is unused
    public const string Licensing = "Licensing"; // [Authorize(Policy = Policies.Licensing)]

    /// <summary>
    /// Policy to restrict access to API endpoints related to the Organization features.
    /// </summary>
    /// <remarks>
    /// <example>
    /// Can be used with the <c>Authorize</c> attribute, for example:
    /// <code>
    /// [Authorize(Policy = Policies.Licensing)]
    /// </code>
    /// </example>
    /// </remarks>
    public const string Organization = "Organization";

    /// <summary>
    /// Policy to restrict access to API endpoints related to the setting up new installations.
    /// </summary>
    /// <remarks>
    /// <example>
    /// Can be used with the <c>Authorize</c> attribute, for example:
    /// <code>
    /// [Authorize(Policy = Policies.Installation)]
    /// </code>
    /// </example>
    /// </remarks>
    public const string Installation = "Installation";

    /// <summary>
    /// Policy to restrict access to API endpoints for Secrets Manager features.
    /// </summary>
    /// <remarks>
    /// <example>
    /// Can be used with the <c>Authorize</c> attribute, for example:
    /// <code>
    /// [Authorize(Policy = Policies.Secrets)]
    /// </code>
    /// </example>
    /// </remarks>
    public const string Secrets = "Secrets";
}
