namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill. Records and <c>init</c> accessors need this type to exist; .NET Standard 2.0, which analyzers must
/// target, predates it.
/// </summary>
internal static class IsExternalInit;
