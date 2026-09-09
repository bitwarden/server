namespace Bit.Core.Platform.Push.Internal;

public interface IPushEngine
{
    Task PushAsync<T>(PushNotification<T> pushNotification)
        where T : class;
}
