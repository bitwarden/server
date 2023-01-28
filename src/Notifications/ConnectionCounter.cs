namespace Bit.Notifications;

public class ConnectionCounter
{
    private int _count = 0;

    public void Increment()
    {
        Interlocked.Increment(ref _count);
    }

    public void Decrement()
    {
        Interlocked.Decrement(ref _count);
    }

    public void Reset()
    {
        _count = 0;
    }

    public int GetCount()
    {
        return _count;
    }
}
