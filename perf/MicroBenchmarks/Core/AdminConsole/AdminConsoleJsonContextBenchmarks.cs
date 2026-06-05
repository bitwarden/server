using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Models.Data;

namespace Bit.MicroBenchmarks.Core.AdminConsole;

/// <summary>
/// Compares source-generated JSON serialization against reflection-based serialization for the
/// types registered in <see cref="AdminConsoleJsonContext"/> and <see cref="ResetPasswordJsonContext"/>.
///
/// The two contexts have different responsibilities:
/// <list type="bullet">
///   <item>
///     <description>
///       <see cref="AdminConsoleJsonContext"/> — camelCase serialization for all types; case-sensitive
///       deserialization for types whose JSON is always written by our own code (e.g. <see cref="Permissions"/>).
///     </description>
///   </item>
///   <item>
///     <description>
///       <see cref="ResetPasswordJsonContext"/> — case-insensitive deserialization for
///       <see cref="ResetPasswordDataModel"/>, which can arrive with PascalCase keys from
///       <c>PolicyDataValidator.ValidateAndSerialize</c>.
///     </description>
///   </item>
/// </list>
///
/// Run with:
///   dotnet run -c Release --project perf/MicroBenchmarks -- --filter *AdminConsoleJsonContext*
/// </summary>
[MemoryDiagnoser]
public class AdminConsoleJsonContextBenchmarks
{
    // Reflection options matching AdminConsoleJsonContext (camelCase, case-sensitive).
    private static readonly JsonSerializerOptions _camelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Reflection options matching ResetPasswordJsonContext (camelCase, case-insensitive).
    private static readonly JsonSerializerOptions _caseInsensitiveOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    // --- Fixture objects ---
    private static readonly ResetPasswordDataModel _resetPasswordModel = new() { AutoEnrollEnabled = true };

    private static readonly Permissions _permissionsModel = new()
    {
        AccessEventLogs = true,
        AccessImportExport = false,
        AccessReports = true,
        CreateNewCollections = false,
        EditAnyCollection = true,
        DeleteAnyCollection = false,
        ManageGroups = true,
        ManagePolicies = false,
        ManageSso = true,
        ManageUsers = false,
        ManageResetPassword = true,
        ManageScim = false,
    };

    // --- JSON fixture strings ---

    // camelCase: written by AdminConsoleJsonContext / SetDataModel<T>
    private static readonly string _resetPasswordCamelCaseJson =
        JsonSerializer.Serialize(_resetPasswordModel, _camelCaseOptions);

    // PascalCase: produced by PolicyDataValidator.ValidateAndSerialize (no naming policy),
    // the scenario that previously relied on JsonHelpers.IgnoreCase.
    private const string ResetPasswordPascalCaseJson = """{"AutoEnrollEnabled":true}""";

    private static readonly string _permissionsJson =
        JsonSerializer.Serialize(_permissionsModel, _camelCaseOptions);

    // =========================================================================
    // Serialization — AdminConsoleJsonContext (all types)
    // =========================================================================

    [Benchmark(Baseline = true)]
    public string Reflection_Serialize_ResetPasswordDataModel() =>
        JsonSerializer.Serialize(_resetPasswordModel, _camelCaseOptions);

    [Benchmark]
    public string SourceGen_Serialize_ResetPasswordDataModel() =>
        JsonSerializer.Serialize(_resetPasswordModel, AdminConsoleJsonContext.Default.ResetPasswordDataModel);

    [Benchmark]
    public string Reflection_Serialize_Permissions() =>
        JsonSerializer.Serialize(_permissionsModel, _camelCaseOptions);

    [Benchmark]
    public string SourceGen_Serialize_Permissions() =>
        JsonSerializer.Serialize(_permissionsModel, AdminConsoleJsonContext.Default.Permissions);

    // =========================================================================
    // Deserialization — AdminConsoleJsonContext (case-sensitive, Permissions)
    // =========================================================================

    [Benchmark]
    public Permissions? Reflection_Deserialize_Permissions() =>
        JsonSerializer.Deserialize<Permissions>(_permissionsJson, _camelCaseOptions);

    [Benchmark]
    public Permissions? SourceGen_Deserialize_Permissions() =>
        JsonSerializer.Deserialize(_permissionsJson, AdminConsoleJsonContext.Default.Permissions);

    // =========================================================================
    // Deserialization — ResetPasswordJsonContext (case-insensitive, ResetPasswordDataModel)
    //
    // The camelCase benchmark represents the normal hot path; PascalCase represents the
    // legacy path via PolicyDataValidator where the JSON casing was not normalised at write time.
    // =========================================================================

    [Benchmark]
    public ResetPasswordDataModel? Reflection_Deserialize_ResetPassword_CamelCase() =>
        JsonSerializer.Deserialize<ResetPasswordDataModel>(_resetPasswordCamelCaseJson, _caseInsensitiveOptions);

    [Benchmark]
    public ResetPasswordDataModel? SourceGen_Deserialize_ResetPassword_CamelCase() =>
        JsonSerializer.Deserialize(
            _resetPasswordCamelCaseJson,
            ResetPasswordJsonContext.Default.ResetPasswordDataModel);

    [Benchmark]
    public ResetPasswordDataModel? Reflection_Deserialize_ResetPassword_PascalCase() =>
        JsonSerializer.Deserialize<ResetPasswordDataModel>(ResetPasswordPascalCaseJson, _caseInsensitiveOptions);

    [Benchmark]
    public ResetPasswordDataModel? SourceGen_Deserialize_ResetPassword_PascalCase() =>
        JsonSerializer.Deserialize(
            ResetPasswordPascalCaseJson,
            ResetPasswordJsonContext.Default.ResetPasswordDataModel);
}
