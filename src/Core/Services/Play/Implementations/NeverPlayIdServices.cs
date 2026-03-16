namespace Bit.Core.Services;

public class NeverPlayIdServices : IPlayIdService
{
    public string? PlayId
    {
        get => null;
        set { }
    }

    public bool InPlay(out string playId)
    {
        playId = string.Empty;
        return false;
    }
}
