using System.Collections.Immutable;
using Bit.RestrictedDependencyBaselines.Analysis;
using Bit.RestrictedDependencyBaselines.Baselines;
using Bitwarden.Server.Sdk.RestrictedDependencies;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bit.RestrictedDependencyBaselines.Test;

public class ObservedUsageReaderTests
{
    private const string Type = "Bit.Core.Services.IUserService";

    private static readonly DiagnosticDescriptor _observation = new(
        ObservationConstants.DiagnosticId, "observation", "{0}", "RestrictedDependencies", DiagnosticSeverity.Info, isEnabledByDefault: true);

    [Fact]
    public void Read_UsageRow_BecomesBudgetEntry()
    {
        var reader = new ObservedUsageReader();
        reader.Read("Billing", [UsageRow(kind: "member", member: "M:Bit.Core.Services.IUserService.CanAccessPremium", site: "M:Bit.Billing.Thing.Do", count: "2", excepted: false, tracked: true)]);

        var baseline = Assert.Single(BaselineBuilder.Build(Analysis(reader)));
        var entry = Assert.Single(baseline.Usages);

        Assert.Equal(new BudgetEntry(DependencyUsageType.Member, "M:Bit.Core.Services.IUserService.CanAccessPremium", "Billing", "M:Bit.Billing.Thing.Do", 2, Tracked: true), entry);
    }

    [Fact]
    public void Read_ExceptedUsageRow_IsNotWrittenToBaseline()
    {
        var reader = new ObservedUsageReader();
        reader.Read("Billing", [UsageRow(kind: "injection", member: null, site: "M:Bit.Billing.Thing.#ctor", count: "1", excepted: true, tracked: false)]);

        var baseline = Assert.Single(BaselineBuilder.Build(Analysis(reader)));

        Assert.Empty(baseline.Usages);
    }

    [Fact]
    public void Read_UsageRowMissingRequiredProperty_Throws()
    {
        var reader = new ObservedUsageReader();
        var row = Diagnostic.Create(_observation, Location.None, ImmutableDictionary<string, string?>.Empty
            .Add(ObservationConstants.Row, ObservationConstants.UsageRow)
            .Add(ObservationConstants.Type, Type)
            .Add(ObservationConstants.UsageKind, "injection"), "incomplete");

        var exception = Assert.Throws<InvalidOperationException>(() => reader.Read("Billing", [row]));

        Assert.Contains($"'{ObservationConstants.Site}'", exception.Message);
    }

    private static SolutionAnalysis Analysis(ObservedUsageReader reader) =>
        reader.ToAnalysis([Type], [], []);

    private static Diagnostic UsageRow(string kind, string? member, string site, string count, bool excepted, bool tracked) =>
        Diagnostic.Create(_observation, Location.None, ImmutableDictionary<string, string?>.Empty
            .Add(ObservationConstants.Row, ObservationConstants.UsageRow)
            .Add(ObservationConstants.Type, Type)
            .Add(ObservationConstants.UsageKind, kind)
            .Add(ObservationConstants.Member, member)
            .Add(ObservationConstants.Site, site)
            .Add(ObservationConstants.Count, count)
            .Add(ObservationConstants.File, "src/Billing/Thing.cs")
            .Add(ObservationConstants.Excepted, ObservationConstants.ToToken(excepted))
            .Add(ObservationConstants.Tracked, ObservationConstants.ToToken(tracked)), "usage");
}
