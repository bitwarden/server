using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

/// <summary>
/// Protects the keyset and filters with Data Protection under a per-partnership purpose, so a token is opaque, can't be
/// altered, and doesn't read back for any other partnership.
/// </summary>
public sealed class PartnershipEntitlementContinuationTokenProtector(IDataProtectionProvider dataProtectionProvider)
    : IPartnershipEntitlementContinuationTokenProtector
{
    public const string DataProtectorPurpose = "PartnershipEntitlementContinuationToken";

    private readonly IDataProtector _dataProtector = dataProtectionProvider.CreateProtector(DataProtectorPurpose);

    public string Protect(ListPartnershipEntitlementsRequest request, PartnershipEntitlementKeyset after)
    {
        var payload = new Payload(request.State, request.RevisedAfter?.Ticks, after.RevisionDate.Ticks, after.Id);
        var protectedBytes = ForPartnership(request.OrganizationPartnershipId)
            .Protect(JsonSerializer.SerializeToUtf8Bytes(payload));
        return Base64Url.EncodeToString(protectedBytes);
    }

    public bool TryUnprotect(ListPartnershipEntitlementsRequest request, out PartnershipEntitlementKeyset after)
    {
        after = default;
        if (string.IsNullOrEmpty(request.ContinuationToken))
        {
            return false;
        }

        Payload? payload;
        try
        {
            var protectedBytes = Base64Url.DecodeFromChars(request.ContinuationToken);
            payload = JsonSerializer.Deserialize<Payload>(
                ForPartnership(request.OrganizationPartnershipId).Unprotect(protectedBytes));
        }
        catch (Exception e) when (e is FormatException or CryptographicException or JsonException)
        {
            return false;
        }

        if (payload is null ||
            payload.State != request.State ||
            payload.RevisedAfterTicks != request.RevisedAfter?.Ticks)
        {
            return false;
        }

        after = new PartnershipEntitlementKeyset(new DateTime(payload.RevisionDateTicks, DateTimeKind.Utc), payload.Id);
        return true;
    }

    private IDataProtector ForPartnership(Guid organizationPartnershipId) =>
        _dataProtector.CreateProtector(organizationPartnershipId.ToString());

    private sealed record Payload(
        [property: JsonPropertyName("s")] PartnershipEntitlementState? State,
        [property: JsonPropertyName("a")] long? RevisedAfterTicks,
        [property: JsonPropertyName("r")] long RevisionDateTicks,
        [property: JsonPropertyName("i")] Guid Id);
}
