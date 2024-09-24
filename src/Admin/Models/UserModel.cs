#nullable enable

using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Admin.Models;

public record UserModel(
    Guid Id,
    string? Name,
    string Email,
    DateTime CreationDate,
    DateTime? PremiumExpirationDate,
    bool Premium,
    short? MaxStorageGb,
    bool EmailVerified,
    bool TwoFactorEnabled,
    DateTime AccountRevisionDate,
    DateTime RevisionDate,
    DateTime? LastEmailChangeDate,
    DateTime? LastKdfChangeDate,
    DateTime? LastKeyRotationDate,
    DateTime? LastPasswordChangeDate,
    GatewayType? Gateway,
    string GatewayCustomerId,
    string GatewaySubscriptionId,
    string LicenseKey)
{
    public static bool IsTwoFactorEnabled(User user,
        IEnumerable<(Guid userId, bool twoFactorIsEnabled)> twoFactorIsEnabledLookup) =>
        twoFactorIsEnabledLookup.FirstOrDefault(x => x.userId == user.Id).twoFactorIsEnabled;

    public static IEnumerable<UserModel> MapUserModels(
        IEnumerable<User> users,
        IEnumerable<(Guid userId, bool twoFactorIsEnabled)> lookup) =>
        users.Select(user => MapUserModel(user, lookup));

    public static UserModel MapUserModel(User user, IEnumerable<(Guid userId, bool twoFactorIsEnabled)> lookup) =>
        new(
            user.Id,
            user.Name,
            user.Email,
            user.CreationDate,
            user.PremiumExpirationDate,
            user.Premium,
            user.MaxStorageGb,
            user.EmailVerified,
            IsTwoFactorEnabled(user, lookup),
            user.AccountRevisionDate,
            user.RevisionDate,
            user.LastEmailChangeDate,
            user.LastKdfChangeDate,
            user.LastKeyRotationDate,
            user.LastPasswordChangeDate,
            user.Gateway,
            user.GatewayCustomerId ?? string.Empty,
            user.GatewaySubscriptionId ?? string.Empty,
            user.LicenseKey ?? string.Empty);

    public static UserModel MapUserModel(User user, bool isTwoFactorEnabled) =>
        new(
            user.Id,
            user.Name,
            user.Email,
            user.CreationDate,
            user.PremiumExpirationDate,
            user.Premium,
            user.MaxStorageGb,
            user.EmailVerified,
            isTwoFactorEnabled,
            user.AccountRevisionDate,
            user.RevisionDate,
            user.LastEmailChangeDate,
            user.LastKdfChangeDate,
            user.LastKeyRotationDate,
            user.LastPasswordChangeDate,
            user.Gateway,
            user.GatewayCustomerId ?? string.Empty,
            user.GatewaySubscriptionId ?? string.Empty,
            user.LicenseKey ?? string.Empty);
};
