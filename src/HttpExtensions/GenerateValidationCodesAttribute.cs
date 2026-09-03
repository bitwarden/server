namespace Bit.HttpExtensions;

/// <summary>
/// Marks a request model whose validation codes should be worked out at build time rather than by reflecting over
/// it at runtime.
/// </summary>
/// <remarks>
/// <para>
/// Needed only where the app must survive trimming or ahead-of-time publishing — a minimal API. MVC declares
/// itself unsupported under both, so a controller's models gain nothing from this and can be left unmarked; they
/// resolve by reflection instead.
/// </para>
/// <para>
/// Marking a type also covers everything reachable from it, so a model whose properties are themselves models
/// needs the attribute only at the root.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateValidationCodesAttribute : Attribute;
