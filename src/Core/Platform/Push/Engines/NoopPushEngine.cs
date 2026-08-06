namespace Bit.Core.Platform.Push.Internal;

internal class NoopPushEngine : IPushEngine
{
    public Task PushAsync<T>(PushNotification<T> pushNotification) where T : class => Task.CompletedTask;
}
