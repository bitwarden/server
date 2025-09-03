using Bit.Core.Enums;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Platform.Push.Internal;

internal class NoopPushEngine : IPushEngine
{
    public Task PushCipherAsync(Cipher cipher, PushType pushType, IEnumerable<Guid>? collectionIds) => Task.CompletedTask;

    public Task PushAsync<T>(PushNotification<T> pushNotification) where T : class => Task.CompletedTask;
}
