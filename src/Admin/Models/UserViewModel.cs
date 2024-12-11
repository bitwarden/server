using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Vault.Entities;

namespace Bit.Admin.Models;

public class UserViewModel
{
    public Guid Id { get; }
    public string Name { get; }
    public string Email { get; }
    public DateTime CreationDate { get; }
    public DateTime? PremiumExpirationDate { get; }
    public bool Premium { get; }
    public short? MaxStorageGb { get; }
    public bool EmailVerified { get; }
    public bool? ClaimedAccount { get; }
    public bool TwoFactorEnabled { get; }
    public DateTime AccountRevisionDate { get; }
    public DateTime RevisionDate { get; }
    public DateTime? LastEmailChangeDate { get; }
    public DateTime? LastKdfChangeDate { get; }
    public DateTime? LastKeyRotationDate { get; }
    public DateTime? LastPasswordChangeDate { get; }
    public GatewayType? Gateway { get; }
    public string GatewayCustomerId { get; }
    public string GatewaySubscriptionId { get; }
    public string LicenseKey { get; }
    public int CipherCount { get; set; }

    public UserViewModel(
        Guid id,
        string name,
        string email,
        DateTime creationDate,
        DateTime? premiumExpirationDate,
        bool premium,
        short? maxStorageGb,
        bool emailVerified,
        bool? claimedAccount,
        bool twoFactorEnabled,
        DateTime accountRevisionDate,
        DateTime revisionDate,
        DateTime? lastEmailChangeDate,
        DateTime? lastKdfChangeDate,
        DateTime? lastKeyRotationDate,
        DateTime? lastPasswordChangeDate,
        GatewayType? gateway,
        string gatewayCustomerId,
        string gatewaySubscriptionId,
        string licenseKey,
        IEnumerable<Cipher> ciphers
    )
    {
        Id = id;
        Name = name;
        Email = email;
        CreationDate = creationDate;
        PremiumExpirationDate = premiumExpirationDate;
        Premium = premium;
        MaxStorageGb = maxStorageGb;
        EmailVerified = emailVerified;
        ClaimedAccount = claimedAccount;
        TwoFactorEnabled = twoFactorEnabled;
        AccountRevisionDate = accountRevisionDate;
        RevisionDate = revisionDate;
        LastEmailChangeDate = lastEmailChangeDate;
        LastKdfChangeDate = lastKdfChangeDate;
        LastKeyRotationDate = lastKeyRotationDate;
        LastPasswordChangeDate = lastPasswordChangeDate;
        Gateway = gateway;
        GatewayCustomerId = gatewayCustomerId;
        GatewaySubscriptionId = gatewaySubscriptionId;
        LicenseKey = licenseKey;
        CipherCount = ciphers.Count();
    }

    public static IEnumerable<UserViewModel> MapViewModels(
        IEnumerable<User> users,
        IEnumerable<(Guid userId, bool twoFactorIsEnabled)> lookup
    ) => users.Select(user => MapViewModel(user, lookup, false));

    public static UserViewModel MapViewModel(
        User user,
        IEnumerable<(Guid userId, bool twoFactorIsEnabled)> lookup,
        bool? claimedAccount
    ) =>
        new(
            user.Id,
            user.Name,
            user.Email,
            user.CreationDate,
            user.PremiumExpirationDate,
            user.Premium,
            user.MaxStorageGb,
            user.EmailVerified,
            claimedAccount,
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
            user.LicenseKey ?? string.Empty,
            Array.Empty<Cipher>()
        );

    public static UserViewModel MapViewModel(User user, bool isTwoFactorEnabled) =>
        MapViewModel(user, isTwoFactorEnabled, Array.Empty<Cipher>(), false);

    public static UserViewModel MapViewModel(
        User user,
        bool isTwoFactorEnabled,
        IEnumerable<Cipher> ciphers,
        bool? claimedAccount
    ) =>
        new(
            user.Id,
            user.Name,
            user.Email,
            user.CreationDate,
            user.PremiumExpirationDate,
            user.Premium,
            user.MaxStorageGb,
            user.EmailVerified,
            claimedAccount,
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
            user.LicenseKey ?? string.Empty,
            ciphers
        );

    public static bool IsTwoFactorEnabled(
        User user,
        IEnumerable<(Guid userId, bool twoFactorIsEnabled)> twoFactorIsEnabledLookup
    ) => twoFactorIsEnabledLookup.FirstOrDefault(x => x.userId == user.Id).twoFactorIsEnabled;
}
