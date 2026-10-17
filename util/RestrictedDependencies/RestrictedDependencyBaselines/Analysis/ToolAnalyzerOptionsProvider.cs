using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Bitwarden.Server.Sdk.RestrictedDependencies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bit.RestrictedDependencyBaselines.Analysis;

/// <summary>
/// Supplies the build properties the analyzer reads, standing in for the global analyzer config
/// the compiler generates from Directory.Build.props. Naming the restricted types is also what
/// puts the analyzer in observe mode: it enforces nothing, because the baseline this tool is about
/// to rewrite cannot be the authority.
/// </summary>
internal sealed class ToolAnalyzerOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly Options _options;

    public ToolAnalyzerOptionsProvider(string repoRoot, ImmutableArray<string> restrictedTypes)
    {
        _options = new Options(repoRoot, restrictedTypes);
    }

    public override AnalyzerConfigOptions GlobalOptions => _options;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;

    private sealed class Options : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values;

        public Options(string repoRoot, ImmutableArray<string> restrictedTypes)
        {
            _values = new Dictionary<string, string>(KeyComparer)
            {
                [AnalyzerConfigConstants.Analysis] = "true",
                [AnalyzerConfigConstants.RepositoryRoot] = repoRoot,
                [AnalyzerConfigConstants.SeedTypes] =
                    string.Join(AnalyzerConfigConstants.SeedTypeSeparator, restrictedTypes),
            };
        }

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => _values.TryGetValue(key, out value);
    }
}
