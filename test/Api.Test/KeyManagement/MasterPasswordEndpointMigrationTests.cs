#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.KeyManagement.Models.Requests;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.KeyManagement.Models.Api.Request;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;
using Xunit.Abstractions;
using IdentityAccountsController = Bit.Identity.Controllers.AccountsController;

namespace Bit.Api.Test.KeyManagement;

/// <summary>
/// Architecture tests that deterministically track all API endpoints and request models
/// accepting master password hash data (legacy or V2 unlock/authentication types).
///
/// Purpose:
///   1. Discovery — enumerate every endpoint and request model that accepts master password data
///   2. Manifest — assert discovered sets match known lists so changes are noticed
///   3. Enforcement — once an endpoint/model is migrated to V2 types, assert those types are required
///
/// Two levels of coverage:
///   - Endpoint-level: scans controllers in the Bit.Api assembly for [FromBody] parameters
///   - Model-level: scans request model types in both Bit.Api and Bit.Core assemblies,
///     catching models like RegisterFinishRequestModel used by controllers in other assemblies
/// </summary>
public class MasterPasswordEndpointMigrationTests
{
    private readonly ITestOutputHelper _output;

    public MasterPasswordEndpointMigrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Detection criteria

    /// <summary>
    /// Property names on request models that indicate legacy master password hash usage.
    /// These are the properties the V2 migration aims to replace.
    /// </summary>
    private static readonly HashSet<string> LegacyPropertyNames = new(StringComparer.Ordinal)
    {
        "MasterPasswordHash",
        "NewMasterPasswordHash",
    };

    /// <summary>
    /// Property names on top-level request models that indicate V2 master password
    /// authentication hash usage (distinct from the V2 composite types).
    /// </summary>
    private static readonly HashSet<string> V2AuthenticationHashPropertyNames = new(StringComparer.Ordinal)
    {
        "OldMasterKeyAuthenticationHash",
    };

    /// <summary>
    /// Property types that indicate V2 unlock/authentication data adoption.
    /// A request model having a property of any of these types means
    /// the endpoint has been (at least partially) migrated to V2.
    /// </summary>
    private static readonly HashSet<Type> V2CompositeTypes = new()
    {
        typeof(MasterPasswordAuthenticationDataRequestModel),
        typeof(MasterPasswordUnlockDataRequestModel),
        typeof(MasterPasswordUnlockAndAuthenticationDataModel),
        typeof(UnlockDataRequestModel),
    };

    #endregion

    #region Known endpoint manifest

    /// <summary>
    /// A set of known endpoint signatures that accept master password data.
    /// Format: "ControllerTypeName.MethodName(BodyParameterTypeName)"
    ///
    /// If <see cref="AllEndpointsAcceptingMasterPasswordData_MatchExpectedManifest"/> fails,
    /// the test output will show which entries to add or remove.
    /// </summary>
    private static readonly HashSet<string> ExpectedEndpoints = new()
    {
        // ── Auth/Api/AccountsController ────────────────────────────────────────────
        "AccountsController.PostPassword(PasswordRequestModel)",
        "AccountsController.PostSetPasswordAsync(SetInitialPasswordRequestModel)",
        "AccountsController.PostKdf(PasswordRequestModel)",
        "AccountsController.PutUpdateTempPasswordAsync(UpdateTempPasswordRequestModel)",
        "AccountsController.PutUpdateTdePasswordAsync(UpdateTdeOffboardingPasswordRequestModel)",

        // ── Auth/Identity/AccountsController ────────────────────────────────────────────
        "AccountsController.PostRegisterFinish(RegisterFinishRequestModel)",

        // ── KeyManagement/AccountsKeyManagementController ──────────────────────
        "AccountsKeyManagementController.RotateUserAccountKeysAsync(RotateUserAccountKeysAndDataRequestModel)",

        // ── Auth/EmergencyAccessController ─────────────────────────────────────
        "EmergencyAccessController.Password(EmergencyAccessPasswordRequestModel)",

        // ── AdminConsole/OrganizationUsersController ───────────────────────────
        "OrganizationUsersController.PutResetPasswordEnrollment(OrganizationUserResetPasswordEnrollmentRequestModel)",
        "OrganizationUsersController.PutResetPassword(OrganizationUserResetPasswordRequestModel)",
    };

    /// <summary>
    /// Endpoints that have been fully migrated to V2 unlock/authentication types.
    /// Once listed here, the enforcement test verifies V2 properties are required
    /// and legacy properties (if still present) are marked <see cref="ObsoleteAttribute"/>.
    ///
    /// Add endpoint signatures here as migration is completed.
    /// </summary>
    private static readonly HashSet<string> FullyMigratedEndpoints = new()
    {
        "AccountsKeyManagementController.RotateUserAccountKeysAsync(RotateUserAccountKeysAndDataRequestModel)",
    };

    /// <summary>
    /// Endpoints that accept master password data but are excluded from tracking.
    /// These are discovered by the scan but intentionally ignored in manifest assertions.
    /// </summary>
    private static readonly HashSet<string> ExcludedEndpoints = new()
    {
        // ── Auth/AccountsController ────────────────────────────────────────────
        "AccountsController.PostEmailToken(EmailTokenRequestModel)",
        "AccountsController.PostEmail(EmailRequestModel)",
        "AccountsController.PostSecurityStamp(SecretVerificationRequestModel)",
        "AccountsController.Delete(SecretVerificationRequestModel)",
        "AccountsController.PostDelete(SecretVerificationRequestModel)",
        "AccountsController.ApiKey(SecretVerificationRequestModel)",
        "AccountsController.RotateApiKey(SecretVerificationRequestModel)",
        "AccountsController.SetUserVerifyDevicesAsync(SetVerifyDevicesRequestModel)",
        "AccountsController.PostSetUserVerifyDevicesAsync(SetVerifyDevicesRequestModel)",
        "AccountsController.ResendNewDeviceOtpAsync(UnauthenticatedSecretVerificationRequestModel)",
        "AccountsController.PostVerifyPassword(SecretVerificationRequestModel)",

        // ── Auth/TwoFactorController ───────────────────────────────────────────
        "TwoFactorController.GetAuthenticator(SecretVerificationRequestModel)",
        "TwoFactorController.PutAuthenticator(UpdateTwoFactorAuthenticatorRequestModel)",
        "TwoFactorController.PostAuthenticator(UpdateTwoFactorAuthenticatorRequestModel)",
        "TwoFactorController.DisableAuthenticator(TwoFactorAuthenticatorDisableRequestModel)",
        "TwoFactorController.GetYubiKey(SecretVerificationRequestModel)",
        "TwoFactorController.PutYubiKey(UpdateTwoFactorYubicoOtpRequestModel)",
        "TwoFactorController.PostYubiKey(UpdateTwoFactorYubicoOtpRequestModel)",
        "TwoFactorController.GetDuo(SecretVerificationRequestModel)",
        "TwoFactorController.PutDuo(UpdateTwoFactorDuoRequestModel)",
        "TwoFactorController.PostDuo(UpdateTwoFactorDuoRequestModel)",
        "TwoFactorController.GetOrganizationDuo(SecretVerificationRequestModel)",
        "TwoFactorController.PutOrganizationDuo(UpdateTwoFactorDuoRequestModel)",
        "TwoFactorController.PostOrganizationDuo(UpdateTwoFactorDuoRequestModel)",
        "TwoFactorController.GetWebAuthn(SecretVerificationRequestModel)",
        "TwoFactorController.GetWebAuthnChallenge(SecretVerificationRequestModel)",
        "TwoFactorController.PutWebAuthn(TwoFactorWebAuthnRequestModel)",
        "TwoFactorController.PostWebAuthn(TwoFactorWebAuthnRequestModel)",
        "TwoFactorController.DeleteWebAuthn(TwoFactorWebAuthnDeleteRequestModel)",
        "TwoFactorController.GetEmail(SecretVerificationRequestModel)",
        "TwoFactorController.SendEmail(TwoFactorEmailRequestModel)",
        "TwoFactorController.SendEmailLoginAsync(TwoFactorEmailRequestModel)",
        "TwoFactorController.PutEmail(UpdateTwoFactorEmailRequestModel)",
        "TwoFactorController.PostEmail(UpdateTwoFactorEmailRequestModel)",
        "TwoFactorController.PutDisable(TwoFactorProviderRequestModel)",
        "TwoFactorController.PostDisable(TwoFactorProviderRequestModel)",
        "TwoFactorController.PutOrganizationDisable(TwoFactorProviderRequestModel)",
        "TwoFactorController.PostOrganizationDisable(TwoFactorProviderRequestModel)",
        "TwoFactorController.GetRecover(SecretVerificationRequestModel)",

        // ── Auth/WebAuthnController ────────────────────────────────────────────
        "WebAuthnController.AttestationOptions(SecretVerificationRequestModel)",
        "WebAuthnController.AssertionOptions(SecretVerificationRequestModel)",
        "WebAuthnController.Delete(SecretVerificationRequestModel)",

        // ── Vault/CiphersController ────────────────────────────────────────────
        "CiphersController.PostPurge(SecretVerificationRequestModel)",

        // ── AdminConsole/OrganizationsController ───────────────────────────────
        "OrganizationsController.Delete(SecretVerificationRequestModel)",
        "OrganizationsController.PostDelete(SecretVerificationRequestModel)",
        "OrganizationsController.ApiKey(OrganizationApiKeyRequestModel)",
        "OrganizationsController.RotateApiKey(OrganizationApiKeyRequestModel)",

        // ── Controllers/DevicesController ──────────────────────────────────────
        "DevicesController.PostUpdateTrust(UpdateDevicesTrustRequestModel)",

        // ── Auth/AuthRequestsController ────────────────────────────────────────
        "AuthRequestsController.Put(AuthRequestUpdateRequestModel)",
    };

    #endregion

    #region Known request model manifest

    /// <summary>
    /// Assemblies to scan for request model types containing master password data.
    /// </summary>
    private static readonly Assembly[] ScannedAssemblies =
    [
        typeof(AccountsController).Assembly,         // Bit.Api
        typeof(RegisterFinishRequestModel).Assembly, // Bit.Core
        typeof(IdentityAccountsController).Assembly, // Bit.Identity
    ];

    /// <summary>
    /// The complete, known set of request model types that contain master password data
    /// (legacy properties, V2 types, or both). Scanned across both Bit.Api and Bit.Core assemblies.
    /// Format: unqualified type name.
    ///
    /// This catches models like <see cref="RegisterFinishRequestModel"/> that live in Core
    /// and are used by controllers in the Identity project (outside the endpoint-level scan).
    ///
    /// If <see cref="AllRequestModelsWithMasterPasswordData_MatchExpectedManifest"/> fails,
    /// the test output will show which entries to add or remove.
    /// </summary>
    private static readonly HashSet<string> ExpectedRequestModels = new()
    {
        // ── Base verification models ───────────────────────────────────────────
        "SecretVerificationRequestModel",

        // ── SecretVerificationRequestModel subclasses (inherit MasterPasswordHash) ─
        "EmailTokenRequestModel",
        "EmailRequestModel",
        "PasswordRequestModel",
        "UnauthenticatedSecretVerificationRequestModel",
        "SetVerifyDevicesRequestModel",
        "UpdateDevicesTrustRequestModel",
        "OrganizationApiKeyRequestModel",

        // ── TwoFactor models (inherit MasterPasswordHash via SecretVerificationRequestModel) ─
        "UpdateTwoFactorAuthenticatorRequestModel",
        "UpdateTwoFactorDuoRequestModel",
        "UpdateTwoFactorYubicoOtpRequestModel",
        "TwoFactorEmailRequestModel",
        "UpdateTwoFactorEmailRequestModel",
        "TwoFactorWebAuthnDeleteRequestModel",
        "TwoFactorWebAuthnRequestModel",
        "TwoFactorProviderRequestModel",
        "TwoFactorAuthenticatorDisableRequestModel",

        // ── Standalone legacy models ───────────────────────────────────────────
        "RegenerateTwoFactorRequestModel",
        "UpdateKeyRequestModel",
        "UpdateTdeOffboardingPasswordRequestModel",
        "EmergencyAccessPasswordRequestModel",
        "OrganizationUserResetPasswordRequestModel",
        "OrganizationUserResetPasswordEnrollmentRequestModel",
        "ResetPasswordWithOrgIdRequestModel",
        "UpdateTempPasswordRequestModel",

        // ── Models with both legacy and V2 (in-progress migration) ─────────────
        "SetInitialPasswordRequestModel",

        // ── V2-only models (fully migrated) ────────────────────────────────────
        "RotateUserAccountKeysAndDataRequestModel",

        // ── Core assembly models (used by Identity controllers) ────────────────
        "RegisterFinishRequestModel",
        "AuthRequestUpdateRequestModel",

        // ── Core entities and data models (internal data flow) ─────────────────
        "AuthRequest",
        "OrganizationAdminAuthRequest",
        "PendingAuthRequestDetails",
        "RotateUserAccountKeysData",

        // ── Response models (API surface — data returned to client) ────────────
        "AuthRequestResponseModel",
        "PendingAuthRequestResponseModel",

        // ── Additional request models ──────────────────────────────────────────
        "TwoFactorRecoveryRequestModel",
    };

    /// <summary>
    /// Request model types that have been fully migrated to V2 unlock/authentication types.
    /// Once listed here, the enforcement test verifies V2 properties are required
    /// and legacy properties (if still present) are marked <see cref="ObsoleteAttribute"/>.
    /// </summary>
    private static readonly HashSet<string> FullyMigratedRequestModels = new()
    {
        "RotateUserAccountKeysAndDataRequestModel",
        "RotateUserAccountKeysData",
    };

    /// <summary>
    /// Request model types that contain master password data but are excluded from tracking.
    /// These are discovered by the scan but intentionally ignored in manifest assertions.
    /// </summary>
    private static readonly HashSet<string> ExcludedRequestModels = new()
    {
    };

    #endregion

    #region Test methods

    /// <summary>
    /// Discovers all endpoints accepting master password data and asserts
    /// they match the known manifest. Fails if an endpoint is added, removed,
    /// or its body parameter type changes without updating <see cref="ExpectedEndpoints"/>.
    /// </summary>
    [Fact]
    public void AllEndpointsAcceptingMasterPasswordData_MatchExpectedManifest()
    {
        var discovered = DiscoverEndpoints().OrderBy(e => e.Signature).ToList();

        // Output full report for developer reference
        _output.WriteLine("=== Master Password Endpoint Migration Report ===\n");
        foreach (var endpoint in discovered)
        {
            _output.WriteLine($"  [{endpoint.Status}] {endpoint.Signature}");
            if (endpoint.LegacyProperties.Count > 0)
            {
                _output.WriteLine($"    Legacy:  {string.Join(", ", endpoint.LegacyProperties)}");
            }

            if (endpoint.V2Properties.Count > 0)
            {
                _output.WriteLine($"    V2:      {string.Join(", ", endpoint.V2Properties.Select(FormatPropertyInfo))}");
            }
        }

        var discoveredSignatures = discovered.Select(e => e.Signature).ToHashSet();
        discoveredSignatures.ExceptWith(ExcludedEndpoints);

        var untracked = discoveredSignatures.Except(ExpectedEndpoints).OrderBy(s => s).ToList();
        var stale = ExpectedEndpoints.Except(discoveredSignatures).OrderBy(s => s).ToList();

        if (untracked.Count > 0)
        {
            _output.WriteLine("\n--- UNTRACKED (add to ExpectedEndpoints or ExcludedEndpoints): ---");
            foreach (var sig in untracked)
            {
                _output.WriteLine($"  \"{sig}\",");
            }
        }

        if (stale.Count > 0)
        {
            _output.WriteLine("\n--- STALE (remove from ExpectedEndpoints): ---");
            foreach (var sig in stale)
            {
                _output.WriteLine($"  \"{sig}\",");
            }
        }

        Assert.True(untracked.Count == 0,
            $"Found {untracked.Count} untracked endpoint(s) accepting master password data. " +
            $"Add to {nameof(ExpectedEndpoints)} or {nameof(ExcludedEndpoints)}:\n" +
            string.Join("\n", untracked.Select(s => $"  \"{s}\",")));

        Assert.True(stale.Count == 0,
            $"Found {stale.Count} stale endpoint(s) in manifest that no longer exist. " +
            $"Remove from {nameof(ExpectedEndpoints)}:\n" +
            string.Join("\n", stale.Select(s => $"  \"{s}\",")));
    }

    /// <summary>
    /// For every endpoint listed in <see cref="FullyMigratedEndpoints"/>, asserts that:
    ///   1. At least one V2 property exists on the request model
    ///   2. All V2 properties are required (non-nullable or [Required])
    ///   3. Any remaining legacy properties are marked [Obsolete]
    ///
    /// Add endpoints to <see cref="FullyMigratedEndpoints"/> as migration completes.
    /// </summary>
    [Fact]
    public void FullyMigratedEndpoints_RequireV2UnlockAndAuthenticationTypes()
    {
        var discovered = DiscoverEndpoints().ToDictionary(e => e.Signature);
        var nullabilityContext = new NullabilityInfoContext();

        foreach (var signature in FullyMigratedEndpoints)
        {
            Assert.True(discovered.ContainsKey(signature),
                $"Endpoint '{signature}' is listed as fully migrated but was not discovered. " +
                $"Remove from {nameof(FullyMigratedEndpoints)} or verify the endpoint still exists.");

            var endpoint = discovered[signature];

            // Must have at least one V2 property
            Assert.True(endpoint.V2Properties.Count > 0,
                $"[{signature}] Listed as fully migrated but has no V2 properties.");

            // All V2 properties must be required (non-nullable or [Required])
            foreach (var v2Prop in endpoint.V2Properties)
            {
                var isRequired = IsPropertyRequired(v2Prop, nullabilityContext);
                Assert.True(isRequired,
                    $"[{signature}] V2 property '{v2Prop.Name}' ({v2Prop.PropertyType.Name}) " +
                    $"must be required (non-nullable or [Required]) on a fully migrated endpoint.");
            }

            // Legacy properties, if still present, must be marked [Obsolete]
            foreach (var legacyName in endpoint.LegacyProperties)
            {
                var legacyProp = endpoint.BodyParameterType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p => p.Name == legacyName);

                if (legacyProp != null)
                {
                    var isObsolete = legacyProp.GetCustomAttribute<ObsoleteAttribute>() != null;
                    Assert.True(isObsolete,
                        $"[{signature}] Legacy property '{legacyName}' still exists and is not " +
                        $"marked [Obsolete]. Either remove it or mark it [Obsolete].");
                }
            }
        }
    }

    /// <summary>
    /// Discovers all request model types (across Bit.Api and Bit.Core assemblies) that contain
    /// master password data properties (legacy or V2). Asserts they match the known manifest.
    ///
    /// This catches models used by controllers in any assembly, including Identity, without
    /// needing a project reference to that assembly.
    /// </summary>
    [Fact]
    public void AllRequestModelsWithMasterPasswordData_MatchExpectedManifest()
    {
        var discovered = DiscoverRequestModels().OrderBy(m => m.Signature).ToList();

        _output.WriteLine("=== Request Model Migration Report ===\n");
        foreach (var model in discovered)
        {
            _output.WriteLine($"  [{model.Status}] {model.Signature}");
            if (model.LegacyProperties.Count > 0)
            {
                _output.WriteLine($"    Legacy:  {string.Join(", ", model.LegacyProperties)}");
            }

            if (model.V2Properties.Count > 0)
            {
                _output.WriteLine($"    V2:      {string.Join(", ", model.V2Properties.Select(FormatPropertyInfo))}");
            }

            if (model.DeclaringAssembly != null)
            {
                _output.WriteLine($"    Assembly: {model.DeclaringAssembly}");
            }
        }

        var discoveredSignatures = discovered.Select(m => m.Signature).ToHashSet();
        discoveredSignatures.ExceptWith(ExcludedRequestModels);

        var untracked = discoveredSignatures.Except(ExpectedRequestModels).OrderBy(s => s).ToList();
        var stale = ExpectedRequestModels.Except(discoveredSignatures).OrderBy(s => s).ToList();

        if (untracked.Count > 0)
        {
            _output.WriteLine("\n--- UNTRACKED (add to ExpectedRequestModels or ExcludedRequestModels): ---");
            foreach (var sig in untracked)
            {
                _output.WriteLine($"  \"{sig}\",");
            }
        }

        if (stale.Count > 0)
        {
            _output.WriteLine("\n--- STALE (remove from ExpectedRequestModels): ---");
            foreach (var sig in stale)
            {
                _output.WriteLine($"  \"{sig}\",");
            }
        }

        Assert.True(untracked.Count == 0,
            $"Found {untracked.Count} untracked request model(s) with master password data. " +
            $"Add to {nameof(ExpectedRequestModels)} or {nameof(ExcludedRequestModels)}:\n" +
            string.Join("\n", untracked.Select(s => $"  \"{s}\",")));

        Assert.True(stale.Count == 0,
            $"Found {stale.Count} stale request model(s) in manifest that no longer match. " +
            $"Remove from {nameof(ExpectedRequestModels)}:\n" +
            string.Join("\n", stale.Select(s => $"  \"{s}\",")));
    }

    /// <summary>
    /// For every request model listed in <see cref="FullyMigratedRequestModels"/>, asserts that:
    ///   1. At least one V2 property exists
    ///   2. All V2 properties are required (non-nullable or [Required])
    ///   3. Any remaining legacy properties are marked [Obsolete]
    /// </summary>
    [Fact]
    public void FullyMigratedRequestModels_RequireV2UnlockAndAuthenticationTypes()
    {
        var discovered = DiscoverRequestModels().ToDictionary(m => m.Signature);
        var nullabilityContext = new NullabilityInfoContext();

        foreach (var signature in FullyMigratedRequestModels)
        {
            Assert.True(discovered.ContainsKey(signature),
                $"Request model '{signature}' is listed as fully migrated but was not discovered. " +
                $"Remove from {nameof(FullyMigratedRequestModels)} or verify the type still exists.");

            var model = discovered[signature];

            Assert.True(model.V2Properties.Count > 0,
                $"[{signature}] Listed as fully migrated but has no V2 properties.");

            foreach (var v2Prop in model.V2Properties)
            {
                var isRequired = IsPropertyRequired(v2Prop, nullabilityContext);
                Assert.True(isRequired,
                    $"[{signature}] V2 property '{v2Prop.Name}' ({v2Prop.PropertyType.Name}) " +
                    $"must be required (non-nullable or [Required]) on a fully migrated model.");
            }

            foreach (var legacyName in model.LegacyProperties)
            {
                var legacyProp = model.ModelType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p => p.Name == legacyName);

                if (legacyProp != null)
                {
                    var isObsolete = legacyProp.GetCustomAttribute<ObsoleteAttribute>() != null;
                    Assert.True(isObsolete,
                        $"[{signature}] Legacy property '{legacyName}' still exists and is not " +
                        $"marked [Obsolete]. Either remove it or mark it [Obsolete].");
                }
            }
        }
    }

    #endregion

    #region Discovery engine

    private static readonly Assembly[] ScannedControllerAssemblies =
    [
        typeof(AccountsController).Assembly,          // Bit.Api
        typeof(IdentityAccountsController).Assembly,  // Bit.Identity
    ];

    private static List<EndpointInfo> DiscoverEndpoints()
    {
        var results = new List<EndpointInfo>();

        var controllerTypes = ScannedControllerAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && IsControllerType(t))
            .OrderBy(t => t.FullName);

        foreach (var controllerType in controllerTypes)
        {
            var actions = controllerType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(HasHttpVerbAttribute);

            foreach (var action in actions)
            {
                var bodyParam = action.GetParameters()
                    .FirstOrDefault(p => p.GetCustomAttribute<FromBodyAttribute>() != null);

                if (bodyParam == null)
                {
                    continue;
                }

                var bodyType = bodyParam.ParameterType;
                var (legacyProps, v2Props) = InspectRequestModelType(bodyType);

                if (legacyProps.Count == 0 && v2Props.Count == 0)
                {
                    continue;
                }

                results.Add(new EndpointInfo(
                    ControllerName: controllerType.Name,
                    ActionName: action.Name,
                    BodyParameterType: bodyType,
                    LegacyProperties: legacyProps,
                    V2Properties: v2Props));
            }
        }

        return results;
    }

    private static List<ModelInfo> DiscoverRequestModels()
    {
        var results = new List<ModelInfo>();

        // Types in V2CompositeTypes are the building blocks, not migration targets
        var excludedTypes = V2CompositeTypes;

        foreach (var assembly in ScannedAssemblies)
        {
            var types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !excludedTypes.Contains(t))
                .OrderBy(t => t.FullName);

            foreach (var type in types)
            {
                var (legacyProps, v2Props) = InspectRequestModelType(type);

                if (legacyProps.Count == 0 && v2Props.Count == 0)
                {
                    continue;
                }

                results.Add(new ModelInfo(
                    ModelType: type,
                    LegacyProperties: legacyProps,
                    V2Properties: v2Props));
            }
        }

        return results;
    }

    /// <summary>
    /// Inspects a request model type (including inherited members) for legacy
    /// master password hash properties and V2 unlock/authentication properties.
    /// </summary>
    private static (List<string> Legacy, List<PropertyInfo> V2) InspectRequestModelType(Type type)
    {
        var legacy = new List<string>();
        var v2 = new List<PropertyInfo>();

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            // Legacy: string properties with known legacy names
            if (prop.PropertyType == typeof(string) && LegacyPropertyNames.Contains(prop.Name))
            {
                legacy.Add(prop.Name);
            }

            // V2: properties whose type is a known V2 composite type
            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            if (V2CompositeTypes.Contains(propType))
            {
                v2.Add(prop);
            }

            // V2: string properties with known V2 authentication hash names
            if (prop.PropertyType == typeof(string) && V2AuthenticationHashPropertyNames.Contains(prop.Name))
            {
                v2.Add(prop);
            }
        }

        return (legacy, v2);
    }

    private static bool IsControllerType(Type type)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current == typeof(Controller) || current == typeof(ControllerBase))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static bool HasHttpVerbAttribute(MethodInfo method)
    {
        return method.GetCustomAttributes(inherit: true)
            .Any(attr => attr is HttpMethodAttribute);
    }

    private static bool IsPropertyRequired(PropertyInfo property, NullabilityInfoContext context)
    {
        // Check [Required] attribute
        if (property.GetCustomAttribute<RequiredAttribute>() != null)
        {
            return true;
        }

        // Check nullability state (non-nullable reference types or `required` keyword)
        try
        {
            var nullabilityInfo = context.Create(property);
            if (nullabilityInfo.WriteState == NullabilityState.NotNull)
            {
                return true;
            }
        }
        catch
        {
            // NullabilityInfoContext may fail on some edge cases; fall through
        }

        return false;
    }

    private static string FormatPropertyInfo(PropertyInfo prop)
    {
        return $"{prop.Name}:{prop.PropertyType.Name}";
    }

    #endregion

    #region Types

    private record EndpointInfo(
        string ControllerName,
        string ActionName,
        Type BodyParameterType,
        List<string> LegacyProperties,
        List<PropertyInfo> V2Properties)
    {
        public string Signature =>
            $"{ControllerName}.{ActionName}({BodyParameterType.Name})";

        public MigrationStatus Status =>
            (LegacyProperties.Count > 0, V2Properties.Count > 0) switch
            {
                (true, true) => MigrationStatus.InProgress,
                (true, false) => MigrationStatus.LegacyOnly,
                (false, true) => MigrationStatus.Migrated,
                _ => MigrationStatus.Unknown,
            };
    }

    private record ModelInfo(
        Type ModelType,
        List<string> LegacyProperties,
        List<PropertyInfo> V2Properties)
    {
        public string Signature => ModelType.Name;

        public string? DeclaringAssembly => ModelType.Assembly.GetName().Name;

        public MigrationStatus Status =>
            (LegacyProperties.Count > 0, V2Properties.Count > 0) switch
            {
                (true, true) => MigrationStatus.InProgress,
                (true, false) => MigrationStatus.LegacyOnly,
                (false, true) => MigrationStatus.Migrated,
                _ => MigrationStatus.Unknown,
            };
    }

    private enum MigrationStatus
    {
        LegacyOnly,
        InProgress,
        Migrated,
        Unknown,
    }

    #endregion
}
