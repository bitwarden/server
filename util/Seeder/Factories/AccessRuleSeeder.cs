using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Utilities;
using Bit.Pam.Entities;

namespace Bit.Seeder.Factories;

internal static class AccessRuleSeeder
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal static AccessRule Create(
        Guid organizationId,
        string name,
        IReadOnlyList<string>? ipAllowlistCidrs = null,
        bool requireHumanApproval = false,
        string? description = null,
        bool enabled = true,
        bool singleActiveLease = false,
        int? defaultLeaseDurationSeconds = null,
        int? maxLeaseDurationSeconds = null,
        bool allowsExtensions = false,
        int? maxExtensionDurationSeconds = null)
    {
        if (allowsExtensions && maxExtensionDurationSeconds is not > 0)
        {
            throw new ArgumentException(
                "maxExtensionDurationSeconds must be positive when allowsExtensions is true.",
                nameof(maxExtensionDurationSeconds));
        }

        return new AccessRule
        {
            Id = CombGuid.Generate(),
            OrganizationId = organizationId,
            Name = name,
            Description = description,
            Conditions = SerializeConditions(ipAllowlistCidrs, requireHumanApproval),
            Enabled = enabled,
            SingleActiveLease = singleActiveLease,
            DefaultLeaseDurationSeconds = defaultLeaseDurationSeconds,
            MaxLeaseDurationSeconds = maxLeaseDurationSeconds,
            AllowsExtensions = allowsExtensions,
            MaxExtensionDurationSeconds = maxExtensionDurationSeconds,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Mirrors the Pam conditions wire format (flat array discriminated by <c>kind</c>), which lives in
    /// bitwarden_license and cannot be referenced from the Seeder.
    /// </summary>
    internal static string SerializeConditions(IReadOnlyList<string>? ipAllowlistCidrs, bool requireHumanApproval)
    {
        var conditions = new List<object>();

        if (ipAllowlistCidrs is not null)
        {
            if (ipAllowlistCidrs.Count == 0)
            {
                throw new ArgumentException(
                    "ipAllowlistCidrs must contain at least one CIDR; omit it for no IP condition.",
                    nameof(ipAllowlistCidrs));
            }

            foreach (var cidr in ipAllowlistCidrs)
            {
                if (string.IsNullOrWhiteSpace(cidr) || !IPNetwork.TryParse(cidr, out _))
                {
                    throw new ArgumentException($"Invalid CIDR: '{cidr}'.", nameof(ipAllowlistCidrs));
                }
            }

            conditions.Add(new IpAllowlistCondition(ipAllowlistCidrs));
        }

        if (requireHumanApproval)
        {
            conditions.Add(new HumanApprovalCondition());
        }

        return JsonSerializer.Serialize(conditions, _jsonOptions);
    }

    private sealed record IpAllowlistCondition(IReadOnlyList<string> Cidrs)
    {
        [JsonPropertyName("kind"), JsonPropertyOrder(-1)]
        public string Kind => "ip_allowlist";
    }

    private sealed record HumanApprovalCondition
    {
        [JsonPropertyName("kind"), JsonPropertyOrder(-1)]
        public string Kind => "human_approval";
    }
}
