#nullable enable

using Bit.Core.Entities;

namespace Bit.Admin.Models;

public record UserModel(
    Guid Id,
    string Email,
    DateTime CreationDate,
    DateTime? PremiumExpirationDate,
    bool Premium,
    short? MaxStorageGb,
    bool EmailVerified,
    bool TwoFactorEnabled)
{
    public static bool IsTwoFactorEnabled(User user, IEnumerable<(Guid userId, bool twoFactorIsEnabled)> twoFactorIsEnabledLookup) =>
        twoFactorIsEnabledLookup.FirstOrDefault(x => x.userId == user.Id).twoFactorIsEnabled;

    public static UserModel MapUserModel(User user, IEnumerable<(Guid userId, bool twoFactorIsEnabled)> lookup) =>
        new(
            user.Id,
            user.Email,
            user.CreationDate,
            user.PremiumExpirationDate,
            user.Premium,
            user.MaxStorageGb,
            user.EmailVerified,
            IsTwoFactorEnabled(user, lookup));
};

