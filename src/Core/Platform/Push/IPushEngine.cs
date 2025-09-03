using Bit.Core.Enums;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Platform.Push.Internal;

public interface IPushEngine
{
    Task PushCipherAsync(Cipher cipher, PushType pushType, IEnumerable<Guid>? collectionIds);

    Task PushAsync<T>(PushNotification<T> pushNotification)
        where T : class;
}
