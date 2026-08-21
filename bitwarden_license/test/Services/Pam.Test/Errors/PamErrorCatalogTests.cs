using System.Text.RegularExpressions;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Services.Pam.Errors;
using Xunit;

namespace Bit.Services.Pam.Test.Errors;

/// <summary>
/// Reflection over every coded PAM error. The point is the codes themselves: they are a published contract, so the
/// invariants that make them usable are asserted over the whole catalog rather than trusted to review.
/// </summary>
public class PamErrorCatalogTests
{
    private static readonly Regex _snakeCase = new("^[a-z][a-z0-9]*(_[a-z0-9]+)*$", RegexOptions.Compiled);

    [Fact]
    public void EveryCode_IsSnakeCase()
    {
        foreach (var error in PamErrorCatalog.Instances())
        {
            Assert.Matches(_snakeCase, error.Type);
        }
    }

    [Fact]
    public void EveryError_CarriesAMessage()
    {
        foreach (var error in PamErrorCatalog.Instances())
        {
            Assert.False(string.IsNullOrWhiteSpace(error.Message), $"{error.GetType().Name} has no message.");
        }
    }

    [Fact]
    public void ACodeReusedAcrossErrors_KeysTheSameProperty()
    {
        // Two errors may share a code when they are the same condition reached from different endpoints (a lease
        // that is no longer active, say). What they must not do is point a client at two different form controls.
        var byCode = PamErrorCatalog.Instances().GroupBy(error => error.Type);

        foreach (var group in byCode)
        {
            Assert.Single(group.Select(error => error.PropertyName).Distinct());
        }
    }

    [Fact]
    public void CodesSharedWithAnotherError_AreDeliberate()
    {
        // A guard, not a rule: sharing is legitimate, but an accidental copy-paste of a code is not, so the set of
        // shared codes is written down. Add to this list only with the reason in the error's own doc comment.
        var shared = PamErrorCatalog.Instances()
            .GroupBy(error => error.Type)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(code => code, StringComparer.Ordinal);

        Assert.Equal(["access_lease_not_active"], shared);
    }

    [Fact]
    public void TheCatalog_CoversTheCodesTheWebClientMatchesToday()
    {
        // The eighteen the clients currently recognise by matching the server's English. Losing any of them
        // silently regresses a client that has stopped carrying its own sentence for it.
        string[] required =
        [
            "access_already_active", "access_request_already_approved", "access_request_already_pending",
            "reason_required", "duration_expected", "window_expected", "window_end_before_start",
            "window_required", "duration_must_be_positive", "duration_exceeds_max", "window_exceeds_max",
            "cipher_not_gated", "rule_name_required", "rule_name_taken", "extension_length_required",
            "collections_missing", "collections_foreign", "collections_already_governed",
        ];
        var codes = PamErrorCatalog.Instances().Select(error => error.Type).ToHashSet();

        Assert.Empty(required.Where(code => !codes.Contains(code)));
    }
}

/// <summary>Finds the PAM error records by reflection so a new one is covered without being registered anywhere.</summary>
internal static class PamErrorCatalog
{
    public static IEnumerable<Type> CodedErrors() =>
        typeof(PamErrorProperties).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, Namespace: "Bit.Services.Pam.Errors" }
                && typeof(IValidationError).IsAssignableFrom(type)
                && typeof(Error).IsAssignableFrom(type));

    public static IEnumerable<IValidationError> Instances() =>
        CodedErrors().Select(type => (IValidationError)Activator.CreateInstance(type, Defaults(type))!);

    /// <summary>Records with a parameter (a bound, a validator sentence) still need an instance to read the code off.</summary>
    private static object?[] Defaults(Type type) =>
        type.GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(string)
                ? "detail"
                : Activator.CreateInstance(parameter.ParameterType))
            .ToArray();
}
