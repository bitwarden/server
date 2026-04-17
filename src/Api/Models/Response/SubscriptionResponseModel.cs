using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Utilities;

namespace Bit.Api.Models.Response;

public class SubscriptionResponseModel : ResponseModel
{
    public SubscriptionResponseModel(User user)
        : base("subscription")
    {
        StorageName = user.Storage.HasValue ? CoreHelpers.ReadableBytesSize(user.Storage.Value) : null;
        StorageGb = user.Storage.HasValue ? Math.Round(user.Storage.Value / 1073741824D, 2) : 0;
        MaxStorageGb = user.MaxStorageGb;
        Expiration = user.PremiumExpirationDate;
    }

    public string? StorageName { get; set; }
    public double? StorageGb { get; set; }
    public short? MaxStorageGb { get; set; }
    public DateTime? Expiration { get; set; }
}
