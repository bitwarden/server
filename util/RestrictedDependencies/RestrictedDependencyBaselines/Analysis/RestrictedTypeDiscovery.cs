using System.Collections.Immutable;
using Bitwarden.Server.Sdk.RestrictedDependencies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Bit.RestrictedDependencyBaselines.Analysis;

/// <summary>
/// Finds restricted types from the source attributes rather than from the existing baselines, so a
/// freshly attributed type is picked up before its baseline exists. The analyzer needs them by
/// name, because a compilation that merely consumes a restricted type has no other way to know it
/// is restricted.
/// </summary>
internal static class RestrictedTypeDiscovery
{
    /// <summary>
    /// Every type in these compilations carrying <c>[RestrictedDependency]</c>, sorted so the
    /// baseline set is deterministic.
    /// </summary>
    public static ImmutableArray<string> Find(IEnumerable<Compilation> compilations)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var compilation in compilations)
        {
            foreach (var type in AllTypes(compilation.Assembly.GlobalNamespace))
            {
                if (!IsRestricted(type))
                {
                    continue;
                }

                var name = MetadataName(type);

                // The analyzer resolves these names with GetTypeByMetadataName. If the name this
                // tool built does not resolve in the very compilation that declares the type, it
                // will not resolve anywhere, and the type would be silently left out of its own
                // baseline rather than reported.
                if (compilation.GetTypeByMetadataName(name) is null)
                {
                    throw new InvalidOperationException(
                        $"Could not build a resolvable metadata name for '{type.ToDisplayString()}' (got '{name}').");
                }

                names.Add(name);
            }
        }

        return [.. names];
    }

    /// <summary>
    /// The workspace normally runs the attribute generator; if it could not load the analyzer, the
    /// attribute text is compiled in directly so the rule on each type still resolves.
    /// </summary>
    public static Compilation EnsureAttributeSource(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName(AttributeConstants.RestrictedDependencyAttributeName) is not null)
        {
            return compilation;
        }

        var parseOptions = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions;
        return compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
            AttributeConstants.Text, parseOptions, path: AttributeConstants.HintName));
    }

    private static bool IsRestricted(ISymbol symbol) =>
        symbol.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == AttributeConstants.RestrictedDependencyAttributeName);

    /// <summary>
    /// The name <see cref="Compilation.GetTypeByMetadataName"/> accepts for <paramref name="type"/>:
    /// dotted namespace, <c>+</c> between nesting levels, arity suffix included. Mirrors what the
    /// analyzer writes into a baseline's <c>type</c> field; <see cref="Find"/> checks the result
    /// round-trips rather than trusting it.
    /// </summary>
    private static string MetadataName(INamedTypeSymbol type)
    {
        if (type.ContainingType is not null)
        {
            return MetadataName(type.ContainingType) + "+" + type.MetadataName;
        }

        var ns = type.ContainingNamespace;
        return ns is null || ns.IsGlobalNamespace
            ? type.MetadataName
            : ns.ToDisplayString() + "." + type.MetadataName;
    }

    private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in NestedTypes(type))
            {
                yield return nested;
            }
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            foreach (var type in AllTypes(child))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> NestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var deeper in NestedTypes(nested))
            {
                yield return deeper;
            }
        }
    }
}
