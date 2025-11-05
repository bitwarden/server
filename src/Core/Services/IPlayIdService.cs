namespace Bit.Core.Services;

public interface IPlayIdService
{
    string? PlayId { get; set; }
    bool InPlay(out string playId);
}
